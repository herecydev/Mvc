// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Microsoft.AspNet.Mvc.Routing;
using Microsoft.AspNet.Mvc.ViewEngines;
using Microsoft.Extensions.OptionsModel;

namespace Microsoft.AspNet.Mvc.Razor
{
    /// <summary>
    /// Default implementation of <see cref="IRazorViewEngine"/>.
    /// </summary>
    /// <remarks>
    /// For <c>ViewResults</c> returned from controllers, views should be located in <see cref="ViewLocationFormats"/>
    /// by default. For the controllers in an area, views should exist in <see cref="AreaViewLocationFormats"/>.
    /// </remarks>
    public class RazorViewEngine : IRazorViewEngine
    {
        private const string ViewExtension = ".cshtml";
        internal const string ControllerKey = "controller";
        internal const string AreaKey = "area";

        private static readonly IEnumerable<string> _viewLocationFormats = new[]
        {
            "/Views/{1}/{0}" + ViewExtension,
            "/Views/Shared/{0}" + ViewExtension,
        };

        private static readonly IEnumerable<string> _areaViewLocationFormats = new[]
        {
            "/Areas/{2}/Views/{1}/{0}" + ViewExtension,
            "/Areas/{2}/Views/Shared/{0}" + ViewExtension,
            "/Views/Shared/{0}" + ViewExtension,
        };

        private readonly IRazorPageFactory _pageFactory;
        private readonly IRazorViewFactory _viewFactory;
        private readonly IList<IViewLocationExpander> _viewLocationExpanders;
        private readonly IViewLocationCache _viewLocationCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="RazorViewEngine" /> class.
        /// </summary>
        /// <param name="pageFactory">The page factory used for creating <see cref="IRazorPage"/> instances.</param>
        public RazorViewEngine(
            IRazorPageFactory pageFactory,
            IRazorViewFactory viewFactory,
            IOptions<RazorViewEngineOptions> optionsAccessor,
            IViewLocationCache viewLocationCache)
        {
            _pageFactory = pageFactory;
            _viewFactory = viewFactory;
            _viewLocationExpanders = optionsAccessor.Value.ViewLocationExpanders;
            _viewLocationCache = viewLocationCache;
        }

        /// <summary>
        /// Gets the locations where this instance of <see cref="RazorViewEngine"/> will search for views.
        /// </summary>
        /// <remarks>
        /// The locations of the views returned from controllers that do not belong to an area.
        /// Locations are composite format strings (see http://msdn.microsoft.com/en-us/library/txafckwd.aspx),
        /// which contains following indexes:
        /// {0} - Action Name
        /// {1} - Controller Name
        /// The values for these locations are case-sensitive on case-senstive file systems.
        /// For example, the view for the <c>Test</c> action of <c>HomeController</c> should be located at
        /// <c>/Views/Home/Test.cshtml</c>. Locations such as <c>/views/home/test.cshtml</c> would not be discovered
        /// </remarks>
        public virtual IEnumerable<string> ViewLocationFormats
        {
            get { return _viewLocationFormats; }
        }

        /// <summary>
        /// Gets the locations where this instance of <see cref="RazorViewEngine"/> will search for views within an
        /// area.
        /// </summary>
        /// <remarks>
        /// The locations of the views returned from controllers that belong to an area.
        /// Locations are composite format strings (see http://msdn.microsoft.com/en-us/library/txafckwd.aspx),
        /// which contains following indexes:
        /// {0} - Action Name
        /// {1} - Controller Name
        /// {2} - Area name
        /// The values for these locations are case-sensitive on case-senstive file systems.
        /// For example, the view for the <c>Test</c> action of <c>HomeController</c> should be located at
        /// <c>/Views/Home/Test.cshtml</c>. Locations such as <c>/views/home/test.cshtml</c> would not be discovered
        /// </remarks>
        public virtual IEnumerable<string> AreaViewLocationFormats
        {
            get { return _areaViewLocationFormats; }
        }

        /// <summary>
        /// Gets the case-normalized route value for the specified route <paramref name="key"/>.
        /// </summary>
        /// <param name="context">The <see cref="ActionContext"/>.</param>
        /// <param name="key">The route key to lookup.</param>
        /// <returns>The value corresponding to the key.</returns>
        /// <remarks>
        /// The casing of a route value in <see cref="ActionContext.RouteData"/> is determined by the client.
        /// This making constructing paths for view locations in a case sensitive file system unreliable. Using the
        /// <see cref="Abstractions.ActionDescriptor.RouteValueDefaults"/> for attribute routes and
        /// <see cref="Abstractions.ActionDescriptor.RouteConstraints"/> for traditional routes to get route values
        /// produces consistently cased results.
        /// </remarks>
        public static string GetNormalizedRouteValue(
            ActionContext context,
            string key)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            object routeValue;
            if (!context.RouteData.Values.TryGetValue(key, out routeValue))
            {
                return null;
            }

            var actionDescriptor = context.ActionDescriptor;
            string normalizedValue = null;
            if (actionDescriptor.AttributeRouteInfo != null)
            {
                object match;
                if (actionDescriptor.RouteValueDefaults.TryGetValue(key, out match))
                {
                    normalizedValue = match?.ToString();
                }
            }
            else
            {
                // Perf: Avoid allocations
                for (var i = 0; i < actionDescriptor.RouteConstraints.Count; i++)
                {
                    var constraint = actionDescriptor.RouteConstraints[i];
                    if (string.Equals(constraint.RouteKey, key, StringComparison.Ordinal))
                    {
                        if (constraint.KeyHandling == RouteKeyHandling.DenyKey)
                        {
                            return null;
                        }
                        else if (constraint.KeyHandling == RouteKeyHandling.RequireKey)
                        {
                            normalizedValue = constraint.RouteValue;
                        }

                        // Duplicate keys in RouteConstraints are not allowed.
                        break;
                    }
                }
            }

            var stringRouteValue = routeValue?.ToString();
            if (string.Equals(normalizedValue, stringRouteValue, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedValue;
            }

            return stringRouteValue;
        }

        /// <inheritdoc />
        public RazorPageResult FindPage(ActionContext context, string pageName, bool isPartial)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (string.IsNullOrEmpty(pageName))
            {
                throw new ArgumentException(Resources.ArgumentCannotBeNullOrEmpty, nameof(pageName));
            }

            return LocatePageFromViewLocations(context, pageName, isPartial);
        }

        /// <inheritdoc />
        public RazorPageResult GetPage(string executingFilePath, string pagePath, bool isPartial)
        {
            if (string.IsNullOrEmpty(pagePath))
            {
                throw new ArgumentException(Resources.ArgumentCannotBeNullOrEmpty, nameof(pagePath));
            }

            if (!(IsApplicationRelativePath(pagePath) || IsRelativePath(pagePath)))
            {
                // Not a path this method can handle.
                return new RazorPageResult(pagePath, Enumerable.Empty<string>());
            }

            var applicationRelativePath = MakePathAbsolute(executingFilePath, pagePath);
            var page = _pageFactory.CreateInstance(applicationRelativePath);
            if (page != null)
            {
                page.IsPartial = isPartial;
                return new RazorPageResult(pagePath, page);
            }

            return new RazorPageResult(pagePath, new[] { applicationRelativePath });
        }

        /// <inheritdoc />
        public ViewEngineResult FindView(ActionContext context, string viewName, bool isPartial)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (string.IsNullOrEmpty(viewName))
            {
                throw new ArgumentException(Resources.ArgumentCannotBeNullOrEmpty, nameof(viewName));
            }

            var pageResult = FindPage(context, viewName, isPartial);
            return CreateViewEngineResult(pageResult, _viewFactory, isPartial);
        }

        /// <inheritdoc />
        public ViewEngineResult GetView(string executingFilePath, string viewPath, bool isPartial)
        {
            if (string.IsNullOrEmpty(viewPath))
            {
                throw new ArgumentException(Resources.ArgumentCannotBeNullOrEmpty, nameof(viewPath));
            }

            var pageResult = GetPage(executingFilePath, viewPath, isPartial);
            return CreateViewEngineResult(pageResult, _viewFactory, isPartial);
        }

        /// <inheritdoc />
        public string MakePathAbsolute(string executingFilePath, string pagePath)
        {
            if (string.IsNullOrEmpty(pagePath))
            {
                // Path is not valid; no change required.
                return pagePath;
            }

            if (IsApplicationRelativePath(pagePath))
            {
                // An absolute path already; no change required.
                return pagePath;
            }

            if (!IsRelativePath(pagePath))
            {
                // A page name; no change required.
                return pagePath;
            }

            // Given a relative path i.e. not yet application-relative (starting with "~/" or "/"), interpret
            // path relative to currently-executing view, if any.
            if (string.IsNullOrEmpty(executingFilePath))
            {
                // Not yet executing a view. Start in app root.
                return "/" + pagePath;
            }

            // Get directory name (including final slash) but do not use Path.GetDirectoryName() to preserve path
            // normalization.
            var index = executingFilePath.LastIndexOf('/');
            Debug.Assert(index >= 0);
            return executingFilePath.Substring(0, index + 1) + pagePath;
        }

        private RazorPageResult LocatePageFromViewLocations(
            ActionContext context,
            string pageName,
            bool isPartial)
        {
            // Initialize the dictionary for the typical case of having controller and action tokens.
            var areaName = GetNormalizedRouteValue(context, AreaKey);

            // Only use the area view location formats if we have an area token.
            var viewLocations = !string.IsNullOrEmpty(areaName) ? AreaViewLocationFormats :
                                                                  ViewLocationFormats;

            var expanderContext = new ViewLocationExpanderContext(context, pageName, isPartial);
            if (_viewLocationExpanders.Count > 0)
            {
                expanderContext.Values = new Dictionary<string, string>(StringComparer.Ordinal);

                // 1. Populate values from viewLocationExpanders.
                // Perf: Avoid allocations
                for( var i = 0; i < _viewLocationExpanders.Count; i++)
                {
                    _viewLocationExpanders[i].PopulateValues(expanderContext);
                }
            }

            // 2. With the values that we've accumulated so far, check if we have a cached result.
            IEnumerable<string> locationsToSearch = null;
            var cachedResult = _viewLocationCache.Get(expanderContext);
            if (!cachedResult.Equals(ViewLocationCacheResult.None))
            {
                if (cachedResult.IsFoundResult)
                {
                    var page = _pageFactory.CreateInstance(cachedResult.ViewLocation);

                    if (page != null)
                    {
                        // 2a We have a cache entry where a view was previously found.
                        return new RazorPageResult(pageName, page);
                    }
                }
                else
                {
                    locationsToSearch = cachedResult.SearchedLocations;
                }
            }

            if (locationsToSearch == null)
            {
                // 2b. We did not find a cached location or did not find a IRazorPage at the cached location.
                // The cached value has expired and we need to look up the page.
                foreach (var expander in _viewLocationExpanders)
                {
                    viewLocations = expander.ExpandViewLocations(expanderContext, viewLocations);
                }

                var controllerName = GetNormalizedRouteValue(context, ControllerKey);

                locationsToSearch = viewLocations.Select(
                    location => string.Format(
                        CultureInfo.InvariantCulture,
                        location,
                        pageName,
                        controllerName,
                        areaName
                    ));
            }

            // 3. Use the expanded locations to look up a page.
            var searchedLocations = new List<string>();
            foreach (var path in locationsToSearch)
            {
                var page = _pageFactory.CreateInstance(path);
                if (page != null)
                {
                    // 3a. We found a page. Cache the set of values that produced it and return a found result.
                    _viewLocationCache.Set(expanderContext, new ViewLocationCacheResult(path, searchedLocations));
                    page.IsPartial = isPartial;
                    return new RazorPageResult(pageName, page);
                }

                searchedLocations.Add(path);
            }

            // 3b. We did not find a page for any of the paths.
            _viewLocationCache.Set(expanderContext, new ViewLocationCacheResult(searchedLocations));
            return new RazorPageResult(pageName, searchedLocations);
        }

        private ViewEngineResult CreateViewEngineResult(
            RazorPageResult result,
            IRazorViewFactory razorViewFactory,
            bool isPartial)
        {
            if (result.SearchedLocations != null)
            {
                return ViewEngineResult.NotFound(result.Name, result.SearchedLocations);
            }

            var view = razorViewFactory.GetView(this, result.Page, isPartial);
            return ViewEngineResult.Found(result.Name, view);
        }

        private static bool IsPagePath(string name)
        {
            // A page path starts with "~" or "/" (if absolute) or ends with ".cshtml" (if relative).
            return IsApplicationRelativePath(name) || IsRelativePath(name);
        }

        private static bool IsApplicationRelativePath(string name)
        {
            Debug.Assert(!string.IsNullOrEmpty(name));
            return name[0] == '~' || name[0] == '/';
        }

        private static bool IsRelativePath(string name)
        {
            Debug.Assert(!string.IsNullOrEmpty(name));

            // Though ./ViewName looks like a relative path, framework searches for that view using view locations.
            return name.EndsWith(ViewExtension, StringComparison.OrdinalIgnoreCase);
        }
    }
}
