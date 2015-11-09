// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNet.Http.Internal;
using Microsoft.AspNet.Mvc.Abstractions;
using Microsoft.AspNet.Mvc.Routing;
using Microsoft.AspNet.Mvc.ViewEngines;
using Microsoft.AspNet.Routing;
using Microsoft.AspNet.Testing;
using Microsoft.Extensions.OptionsModel;
using Moq;
using Xunit;

namespace Microsoft.AspNet.Mvc.Razor.Test
{
    public class RazorViewEngineTest
    {
        private static readonly Dictionary<string, object> _areaTestContext = new Dictionary<string, object>()
        {
            {"area", "foo"},
            {"controller", "bar"},
        };

        private static readonly Dictionary<string, object> _controllerTestContext = new Dictionary<string, object>()
        {
            {"controller", "bar"},
        };

        public static IEnumerable<string[]> AbsoluteViewPathData
        {
            get
            {
                yield return new[] { "~/foo/bar" };
                yield return new[] { "/foo/bar" };
                yield return new[] { "~/foo/bar.txt" };
                yield return new[] { "/foo/bar.txt" };
            }
        }

        public static IEnumerable<object[]> ViewLocationExpanderTestData
        {
            get
            {
                yield return new object[]
                {
                    _controllerTestContext,
                    new[]
                    {
                        "/Views/{1}/{0}.cshtml",
                        "/Views/Shared/{0}.cshtml"
                    }
                };

                yield return new object[]
                {
                    _areaTestContext,
                    new[]
                    {
                        "/Areas/{2}/Views/{1}/{0}.cshtml",
                        "/Areas/{2}/Views/Shared/{0}.cshtml",
                        "/Views/Shared/{0}.cshtml"
                    }
                };
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void FindView_ThrowsIfViewNameIsNullOrEmpty(string viewName)
        {
            // Arrange
            var viewEngine = CreateViewEngine();
            var context = GetActionContext(_controllerTestContext);

            // Act & Assert
            ExceptionAssert.ThrowsArgumentNullOrEmpty(
                () => viewEngine.FindView(context, viewName, isPartial: false),
                "viewName");
        }

        [Theory]
        [MemberData(nameof(AbsoluteViewPathData))]
        public void FindView_WithFullPath_ReturnsNotFound(string viewName)
        {
            // Arrange
            var viewEngine = CreateSuccessfulViewEngine();
            var context = GetActionContext(_controllerTestContext);

            // Act
            var result = viewEngine.FindView(context, viewName, isPartial: false);

            // Assert
            Assert.False(result.Success);
        }

        [Theory]
        [MemberData(nameof(AbsoluteViewPathData))]
        public void FindView_WithFullPathAndCshtmlEnding_ReturnsNotFound(string viewName)
        {
            // Arrange
            var viewEngine = CreateSuccessfulViewEngine();
            var context = GetActionContext(_controllerTestContext);
            viewName += ".cshtml";

            // Act
            var result = viewEngine.FindView(context, viewName, isPartial: false);

            // Assert
            Assert.False(result.Success);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void FindView_WithRelativePath_ReturnsNotFound(bool isPartial)
        {
            // Arrange
            var viewEngine = CreateSuccessfulViewEngine();
            var context = GetActionContext(_controllerTestContext);

            // Act
            var result = viewEngine.FindView(context, "View.cshtml", isPartial);

            // Assert
            Assert.False(result.Success);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GetView_WithViewName_ReturnsNotFound(bool isPartial)
        {
            // Arrange
            var viewEngine = CreateSuccessfulViewEngine();

            // Act
            var result = viewEngine.GetView("~/Home/View1.cshtml", "View2", isPartial);

            // Assert
            Assert.False(result.Success);
        }

        [Fact]
        public void FindView_IsPartial_ReturnsRazorView_IfLookupWasSuccessful()
        {
            // Arrange
            var pageFactory = new Mock<IRazorPageFactory>();
            var viewFactory = new Mock<IRazorViewFactory>();
            var view = Mock.Of<IView>();

            pageFactory
                .Setup(p => p.CreateInstance(It.IsAny<string>()))
                .Returns(Mock.Of<IRazorPage>());
            viewFactory
                .Setup(p => p.GetView(It.IsAny<IRazorViewEngine>(), It.IsAny<IRazorPage>(), true))
                .Returns(view)
                .Verifiable();

            var viewEngine = CreateViewEngine(pageFactory.Object, viewFactory.Object);
            var context = GetActionContext(_controllerTestContext);

            // Act
            var result = viewEngine.FindView(context, "test-view", isPartial: true);

            // Assert
            Assert.True(result.Success);
            Assert.Same(view, result.View);
            Assert.Equal("test-view", result.ViewName);
            viewFactory.Verify();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void FindView_IsPartial_ThrowsIfViewNameIsNullOrEmpty(string partialViewName)
        {
            // Arrange
            var viewEngine = CreateViewEngine();
            var context = GetActionContext(_controllerTestContext);

            // Act & Assert
            ExceptionAssert.ThrowsArgumentNullOrEmpty(
                () => viewEngine.FindView(context, partialViewName, isPartial: true),
                "viewName");
        }

        [Theory]
        [MemberData(nameof(AbsoluteViewPathData))]
        public void FindView_IsPartialWithFullPath_ReturnsNotFound(string partialViewName)
        {
            // Arrange
            var viewEngine = CreateSuccessfulViewEngine();
            var context = GetActionContext(_controllerTestContext);

            // Act
            var result = viewEngine.FindView(context, partialViewName, isPartial: true);

            // Assert
            Assert.False(result.Success);
        }

        [Theory]
        [MemberData(nameof(AbsoluteViewPathData))]
        public void FindView_IsPartialWithFullPathAndCshtmlEnding_ReturnsNotFound(string partialViewName)
        {
            // Arrange
            var viewEngine = CreateSuccessfulViewEngine();
            var context = GetActionContext(_controllerTestContext);
            partialViewName += ".cshtml";

            // Act
            var result = viewEngine.FindView(context, partialViewName, isPartial: true);

            // Assert
            Assert.False(result.Success);
        }

        [Fact]
        public void FindView_IsPartial_FailsButSearchesCorrectLocations_WithAreas()
        {
            // Arrange
            var viewEngine = CreateViewEngine();
            var context = GetActionContext(_areaTestContext);

            // Act
            var result = viewEngine.FindView(context, "partial", isPartial: true);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(new[]
            {
                "/Areas/foo/Views/bar/partial.cshtml",
                "/Areas/foo/Views/Shared/partial.cshtml",
                "/Views/Shared/partial.cshtml",
            }, result.SearchedLocations);
        }

        [Fact]
        public void FindView_IsPartial_FailsButSearchesCorrectLocations_WithoutAreas()
        {
            // Arrange
            var viewEngine = CreateViewEngine();
            var context = GetActionContext(_controllerTestContext);

            // Act
            var result = viewEngine.FindView(context, "partialNoArea", isPartial: true);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(new[] {
                "/Views/bar/partialNoArea.cshtml",
                "/Views/Shared/partialNoArea.cshtml",
            }, result.SearchedLocations);
        }

        [Fact]
        public void FindView_FailsButSearchesCorrectLocationsWithAreas()
        {
            // Arrange
            var viewEngine = CreateViewEngine();
            var context = GetActionContext(_areaTestContext);

            // Act
            var result = viewEngine.FindView(context, "full", isPartial: false);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(new[] {
                "/Areas/foo/Views/bar/full.cshtml",
                "/Areas/foo/Views/Shared/full.cshtml",
                "/Views/Shared/full.cshtml",
            }, result.SearchedLocations);
        }

        [Fact]
        public void FindView_FailsButSearchesCorrectLocationsWithoutAreas()
        {
            // Arrange
            var viewEngine = CreateViewEngine();
            var context = GetActionContext(_controllerTestContext);

            // Act
            var result = viewEngine.FindView(context, "fullNoArea", isPartial: false);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(new[] {
                "/Views/bar/fullNoArea.cshtml",
                "/Views/Shared/fullNoArea.cshtml",
            }, result.SearchedLocations);
        }

        [Fact]
        public void FindView_ReturnsRazorView_IfLookupWasSuccessful()
        {
            // Arrange
            var pageFactory = new Mock<IRazorPageFactory>();
            var viewFactory = new Mock<IRazorViewFactory>();
            var view = Mock.Of<IView>();

            pageFactory
                .Setup(p => p.CreateInstance(It.IsAny<string>()))
                .Returns(Mock.Of<IRazorPage>());
            viewFactory
                .Setup(p => p.GetView(It.IsAny<IRazorViewEngine>(), It.IsAny<IRazorPage>(), false))
                .Returns(view)
                .Verifiable();

            var viewEngine = CreateViewEngine(pageFactory.Object, viewFactory.Object);
            var context = GetActionContext(_controllerTestContext);

            // Act
            var result = viewEngine.FindView(context, "test-view", isPartial: false);

            // Assert
            Assert.True(result.Success);
            Assert.Same(view, result.View);
            Assert.Equal("test-view", result.ViewName);
            viewFactory.Verify();
        }

        [Fact]
        public void FindView_UsesViewLocationFormat_IfRouteDoesNotContainArea()
        {
            // Arrange
            var pageFactory = new Mock<IRazorPageFactory>();
            var viewFactory = new Mock<IRazorViewFactory>();
            var page = Mock.Of<IRazorPage>();
            pageFactory
                .Setup(p => p.CreateInstance("fake-path1/bar/test-view.rzr"))
                .Returns(page)
                .Verifiable();
            var viewEngine = new OverloadedLocationViewEngine(
                pageFactory.Object,
                viewFactory.Object,
                GetOptionsAccessor(),
                GetViewLocationCache());
            var context = GetActionContext(_controllerTestContext);
            viewFactory.Setup(v => v.GetView(viewEngine, page, false))
                .Returns(Mock.Of<IView>());

            // Act
            var result = viewEngine.FindView(context, "test-view", isPartial: false);

            // Assert
            pageFactory.Verify();
        }

        [Fact]
        public void FindView_UsesAreaViewLocationFormat_IfRouteContainsArea()
        {
            // Arrange
            var pageFactory = new Mock<IRazorPageFactory>();
            var page = Mock.Of<IRazorPage>();
            pageFactory
                .Setup(p => p.CreateInstance("fake-area-path/foo/bar/test-view2.rzr"))
                .Returns(page)
                .Verifiable();

            var viewFactory = new Mock<IRazorViewFactory>();
            var viewEngine = new OverloadedLocationViewEngine(
                pageFactory.Object,
                viewFactory.Object,
                GetOptionsAccessor(),
                GetViewLocationCache());
            viewFactory
                .Setup(v => v.GetView(viewEngine, page, false))
                .Returns(Mock.Of<IView>());

            var context = GetActionContext(_areaTestContext);

            // Act
            var result = viewEngine.FindView(context, "test-view2", isPartial: false);

            // Assert
            pageFactory.Verify();
        }

        [Theory]
        [InlineData("Test-View.cshtml")]
        [InlineData("/Home/Test-View.cshtml")]
        public void GetView_DoesNotUseViewLocationFormat_WithRelativePath_IfRouteDoesNotContainArea(string viewName)
        {
            // Arrange
            var expectedViewName = "/Home/Test-View.cshtml";
            var pageFactory = new Mock<IRazorPageFactory>();
            var page = Mock.Of<IRazorPage>();
            pageFactory
                .Setup(p => p.CreateInstance(expectedViewName))
                .Returns(page)
                .Verifiable();

            var viewFactory = new Mock<IRazorViewFactory>();
            var viewEngine = new OverloadedLocationViewEngine(
                pageFactory.Object,
                viewFactory.Object,
                GetOptionsAccessor(),
                GetViewLocationCache());
            viewFactory
                .Setup(v => v.GetView(viewEngine, page, false))
                .Returns(Mock.Of<IView>());

            // Act
            var result = viewEngine.GetView("/Home/Page.cshtml", viewName, isPartial: false);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(expectedViewName, result.ViewName);
            pageFactory.Verify();
        }

        [Theory]
        [InlineData("Test-View.cshtml")]
        [InlineData("/Home/Test-View.cshtml")]
        public void GetView_DoesNotUseViewLocationFormat_WithRelativePath_IfRouteContainArea(string viewName)
        {
            // Arrange
            var expectedViewName = "/Home/Test-View.cshtml";
            var pageFactory = new Mock<IRazorPageFactory>();
            var page = Mock.Of<IRazorPage>();
            pageFactory
                .Setup(p => p.CreateInstance(expectedViewName))
                .Returns(page)
                .Verifiable();

            var viewFactory = new Mock<IRazorViewFactory>();
            var viewEngine = new OverloadedLocationViewEngine(
                pageFactory.Object,
                viewFactory.Object,
                GetOptionsAccessor(),
                GetViewLocationCache());
            viewFactory
                .Setup(v => v.GetView(viewEngine, page, false))
                .Returns(Mock.Of<IView>());

            // Act
            var result = viewEngine.GetView("/Home/Page.cshtml", viewName, isPartial: false);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(expectedViewName, result.ViewName);
            pageFactory.Verify();
        }

        [Theory]
        [InlineData("/Test-View.cshtml")]
        [InlineData("~/Test-View.CSHTML")]
        [InlineData("/Home/Test-View.CSHTML")]
        [InlineData("~/Home/Test-View.cshtml")]
        [InlineData("~/SHARED/TEST-VIEW.CSHTML")]
        public void GetView_UsesGivenPath_WithAppRelativePath(string viewName)
        {
            // Arrange
            var pageFactory = new Mock<IRazorPageFactory>();
            var page = Mock.Of<IRazorPage>();
            pageFactory
                .Setup(p => p.CreateInstance(viewName))
                .Returns(page)
                .Verifiable();

            var viewFactory = new Mock<IRazorViewFactory>();
            var viewEngine = new OverloadedLocationViewEngine(
                pageFactory.Object,
                viewFactory.Object,
                GetOptionsAccessor(),
                GetViewLocationCache());
            viewFactory
                .Setup(v => v.GetView(viewEngine, page, false))
                .Returns(Mock.Of<IView>());

            // Act
            var result = viewEngine.GetView(executingFilePath: null, viewPath: viewName, isPartial: false);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(viewName, result.ViewName);
            pageFactory.Verify();
        }

        [Theory]
        [InlineData("Test-View.cshtml")]
        [InlineData("Test-View.CSHTML")]
        [InlineData("PATH/TEST-VIEW.CSHTML")]
        [InlineData("Path1/Path2/Test-View.cshtml")]
        public void GetView_ResolvesRelativeToCurrentPage_WithRelativePath(string viewName)
        {
            // Arrange
            var expectedViewName = $"/Home/{ viewName }";
            var pageFactory = new Mock<IRazorPageFactory>();
            var page = Mock.Of<IRazorPage>();
            pageFactory
                .Setup(p => p.CreateInstance(expectedViewName))
                .Returns(page)
                .Verifiable();

            var viewFactory = new Mock<IRazorViewFactory>();
            var viewEngine = new OverloadedLocationViewEngine(
                pageFactory.Object,
                viewFactory.Object,
                GetOptionsAccessor(),
                GetViewLocationCache());
            viewFactory
                .Setup(v => v.GetView(viewEngine, page, false))
                .Returns(Mock.Of<IView>());

            // Act
            var result = viewEngine.GetView("/Home/Page.cshtml", viewName, isPartial: false);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(expectedViewName, result.ViewName);
            pageFactory.Verify();
        }

        [Theory]
        [InlineData("Test-View.cshtml")]
        [InlineData("Test-View.CSHTML")]
        [InlineData("PATH/TEST-VIEW.CSHTML")]
        [InlineData("Path1/Path2/Test-View.cshtml")]
        public void GetView_ResolvesRelativeToAppRoot_WithRelativePath_IfNoPageExecuting(string viewName)
        {
            // Arrange
            var expectedViewName = $"/{ viewName }";
            var pageFactory = new Mock<IRazorPageFactory>();
            var page = Mock.Of<IRazorPage>();
            pageFactory
                .Setup(p => p.CreateInstance(expectedViewName))
                .Returns(page)
                .Verifiable();

            var viewFactory = new Mock<IRazorViewFactory>();
            var viewEngine = new OverloadedLocationViewEngine(
                pageFactory.Object,
                viewFactory.Object,
                GetOptionsAccessor(),
                GetViewLocationCache());
            viewFactory
                .Setup(v => v.GetView(viewEngine, page, false))
                .Returns(Mock.Of<IView>());

            // Act
            var result = viewEngine.GetView(executingFilePath: null, viewPath: viewName, isPartial: false);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(expectedViewName, result.ViewName);
            pageFactory.Verify();
        }

        [Theory]
        [MemberData(nameof(ViewLocationExpanderTestData))]
        public void FindView_UsesViewLocationExpandersToLocateViews(
            IDictionary<string, object> routeValues,
            IEnumerable<string> expectedSeeds)
        {
            // Arrange
            var pageFactory = new Mock<IRazorPageFactory>();
            pageFactory
                .Setup(p => p.CreateInstance("test-string/bar.cshtml"))
                .Returns(Mock.Of<IRazorPage>())
                .Verifiable();

            var viewFactory = new Mock<IRazorViewFactory>();
            viewFactory
                .Setup(p => p.GetView(It.IsAny<IRazorViewEngine>(), It.IsAny<IRazorPage>(), It.IsAny<bool>()))
                .Returns(Mock.Of<IView>());

            var expander1Result = new[] { "some-seed" };
            var expander1 = new Mock<IViewLocationExpander>();
            expander1
                .Setup(e => e.PopulateValues(It.IsAny<ViewLocationExpanderContext>()))
                .Callback((ViewLocationExpanderContext c) =>
                {
                    Assert.NotNull(c.ActionContext);
                    c.Values["expander-key"] = expander1.ToString();
                })
                .Verifiable();
            expander1
                .Setup(e => e.ExpandViewLocations(
                    It.IsAny<ViewLocationExpanderContext>(),
                    It.IsAny<IEnumerable<string>>()))
                .Callback((ViewLocationExpanderContext c, IEnumerable<string> seeds) =>
                {
                    Assert.NotNull(c.ActionContext);
                    Assert.Equal(expectedSeeds, seeds);
                })
                .Returns(expander1Result)
                .Verifiable();

            var expander2 = new Mock<IViewLocationExpander>();
            expander2
                .Setup(e => e.ExpandViewLocations(
                    It.IsAny<ViewLocationExpanderContext>(),
                    It.IsAny<IEnumerable<string>>()))
                .Callback((ViewLocationExpanderContext c, IEnumerable<string> seeds) =>
                {
                    Assert.Equal(expander1Result, seeds);
                })
                .Returns(new[] { "test-string/{1}.cshtml" })
                .Verifiable();

            var viewEngine = CreateViewEngine(
                pageFactory.Object,
                viewFactory.Object,
                new[] { expander1.Object, expander2.Object });
            var context = GetActionContext(routeValues);

            // Act
            var result = viewEngine.FindView(context, "test-view", isPartial: false);

            // Assert
            Assert.True(result.Success);
            Assert.IsAssignableFrom<IView>(result.View);
            pageFactory.Verify();
            expander1.Verify();
            expander2.Verify();
        }

        [Fact]
        public void FindView_CachesValuesIfViewWasFound()
        {
            // Arrange
            var pageFactory = new Mock<IRazorPageFactory>();
            pageFactory
                .Setup(p => p.CreateInstance("/Views/bar/baz.cshtml"))
                .Verifiable();
            pageFactory
                .Setup(p => p.CreateInstance("/Views/Shared/baz.cshtml"))
                .Returns(Mock.Of<IRazorPage>())
                .Verifiable();

            var viewFactory = new Mock<IRazorViewFactory>();
            viewFactory
                .Setup(p => p.GetView(It.IsAny<IRazorViewEngine>(), It.IsAny<IRazorPage>(), false))
                .Returns(Mock.Of<IView>());

            var cache = GetViewLocationCache();
            var cacheMock = Mock.Get(cache);
            cacheMock
                .Setup(c => c.Set(It.IsAny<ViewLocationExpanderContext>(), It.IsAny<ViewLocationCacheResult>()))
                .Callback((ViewLocationExpanderContext _, ViewLocationCacheResult cacheResult) =>
                {
                    Assert.Equal("/Views/Shared/baz.cshtml", cacheResult.ViewLocation);
                    Assert.Equal(new[] { "/Views/bar/baz.cshtml" }, cacheResult.SearchedLocations);
                })
                .Verifiable();

            var viewEngine = CreateViewEngine(pageFactory.Object, viewFactory.Object, cache: cache);
            var context = GetActionContext(_controllerTestContext);

            // Act
            var result = viewEngine.FindView(context, "baz", isPartial: false);

            // Assert
            Assert.True(result.Success);
            pageFactory.Verify();
            cacheMock.Verify();
        }

        [Fact]
        public void FindView_UsesCachedValueIfViewWasFound()
        {
            // Arrange
            var pageFactory = new Mock<IRazorPageFactory>(MockBehavior.Strict);
            pageFactory
                .Setup(p => p.CreateInstance("some-view-location"))
                .Returns(Mock.Of<IRazorPage>())
                .Verifiable();

            var viewFactory = new Mock<IRazorViewFactory>();
            viewFactory
                .Setup(p => p.GetView(It.IsAny<IRazorViewEngine>(), It.IsAny<IRazorPage>(), false))
                .Returns(Mock.Of<IView>());

            var expander = new Mock<IViewLocationExpander>(MockBehavior.Strict);
            expander
                .Setup(v => v.PopulateValues(It.IsAny<ViewLocationExpanderContext>()))
                .Verifiable();
            var cacheMock = new Mock<IViewLocationCache>();
            cacheMock
                .Setup(c => c.Get(It.IsAny<ViewLocationExpanderContext>()))
                .Returns(new ViewLocationCacheResult("some-view-location", Enumerable.Empty<string>()))
                .Verifiable();

            var viewEngine = CreateViewEngine(
                pageFactory.Object,
                viewFactory.Object,
                new[] { expander.Object },
                cacheMock.Object);
            var context = GetActionContext(_controllerTestContext);

            // Act
            var result = viewEngine.FindView(context, "baz", isPartial: false);

            // Assert
            Assert.True(result.Success);
            pageFactory.Verify();
            cacheMock.Verify();
            expander.Verify();
        }

        [Fact]
        public void FindView_LooksForViewsIfCachedViewDoesNotExist()
        {
            // Arrange
            var pageFactory = new Mock<IRazorPageFactory>();
            pageFactory
                .Setup(p => p.CreateInstance("expired-location"))
                .Returns((IRazorPage)null)
                .Verifiable();
            pageFactory
                .Setup(p => p.CreateInstance("some-view-location"))
                .Returns(Mock.Of<IRazorPage>())
                .Verifiable();

            var viewFactory = new Mock<IRazorViewFactory>();
            viewFactory
                .Setup(p => p.GetView(It.IsAny<IRazorViewEngine>(), It.IsAny<IRazorPage>(), false))
                .Returns(Mock.Of<IView>());

            var cacheMock = new Mock<IViewLocationCache>();
            cacheMock
                .Setup(c => c.Get(It.IsAny<ViewLocationExpanderContext>()))
                .Returns(new ViewLocationCacheResult("expired-location", Enumerable.Empty<string>()));

            var expander = new Mock<IViewLocationExpander>();
            expander
                .Setup(v => v.PopulateValues(It.IsAny<ViewLocationExpanderContext>()))
                .Verifiable();
            var expanderResult = new[] { "some-view-location" };
            expander
                .Setup(v => v.ExpandViewLocations(
                    It.IsAny<ViewLocationExpanderContext>(),
                    It.IsAny<IEnumerable<string>>()))
                .Returns((ViewLocationExpanderContext c, IEnumerable<string> seed) => expanderResult)
                .Verifiable();

            var viewEngine = CreateViewEngine(
                pageFactory.Object,
                viewFactory.Object,
                expanders: new[] { expander.Object },
                cache: cacheMock.Object);
            var context = GetActionContext(_controllerTestContext);

            // Act
            var result = viewEngine.FindView(context, "baz", isPartial: false);

            // Assert
            Assert.True(result.Success);
            pageFactory.Verify();
            cacheMock.Verify();
            expander.Verify();
        }

        // This test validates an important perf scenario of RazorViewEngine not constructing
        // multiple strings for views that do not exist in the file system on a per-request basis.
        [Fact]
        public void FindView_DoesNotInvokeExpandViewLocations_IfCacheEntryMatchesButViewIsNotFound()
        {
            // Arrange
            var pageFactory = Mock.Of<IRazorPageFactory>();
            var viewFactory = Mock.Of<IRazorViewFactory>();
            var cache = new DefaultViewLocationCache();
            var expander = new Mock<IViewLocationExpander>();
            var expandedLocations = new[]
            {
                "viewlocation1",
                "viewlocation2",
                "viewlocation3",
            };
            expander
                .Setup(v => v.PopulateValues(It.IsAny<ViewLocationExpanderContext>()))
                .Callback((ViewLocationExpanderContext expanderContext) =>
                {
                    expanderContext.Values["somekey"] = "somevalue";
                })
                .Verifiable();
            expander
                .Setup(v => v.ExpandViewLocations(
                    It.IsAny<ViewLocationExpanderContext>(),
                    It.IsAny<IEnumerable<string>>()))
                .Returns((ViewLocationExpanderContext c, IEnumerable<string> viewLocations) => expandedLocations)
                .Verifiable();


            var viewEngine = CreateViewEngine(
                pageFactory,
                viewFactory,
                expanders: new[] { expander.Object },
                cache: cache);
            var context = GetActionContext(_controllerTestContext);

            // Act - 1
            var result = viewEngine.FindView(context, "myview", isPartial: false);

            // Assert - 1
            Assert.False(result.Success);
            Assert.Equal(expandedLocations, result.SearchedLocations);
            expander.Verify();

            // Act - 2
            result = viewEngine.FindView(context, "myview", isPartial: false);

            // Assert - 2
            Assert.False(result.Success);
            Assert.Equal(expandedLocations, result.SearchedLocations);
            expander.Verify(
                v => v.PopulateValues(It.IsAny<ViewLocationExpanderContext>()),
                Times.Exactly(2));
            expander.Verify(
                v => v.ExpandViewLocations(It.IsAny<ViewLocationExpanderContext>(), It.IsAny<IEnumerable<string>>()),
                Times.Once());
        }

        [Theory]
        [MemberData(nameof(AbsoluteViewPathData))]
        public void FindPage_WithFullPath_ReturnsNotFound(string viewName)
        {
            // Arrange
            var viewEngine = CreateSuccessfulViewEngine();
            var context = GetActionContext(_controllerTestContext);

            // Act
            var result = viewEngine.FindPage(context, viewName, isPartial: false);

            // Assert
            Assert.Null(result.Page);
        }

        [Theory]
        [MemberData(nameof(AbsoluteViewPathData))]
        public void FindPage_WithFullPathAndCshtmlEnding_ReturnsNotFound(string viewName)
        {
            // Arrange
            var viewEngine = CreateSuccessfulViewEngine();
            var context = GetActionContext(_controllerTestContext);
            viewName += ".cshtml";

            // Act
            var result = viewEngine.FindPage(context, viewName, isPartial: false);

            // Assert
            Assert.Null(result.Page);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void FindPage_WithRelativePath_ReturnsNotFound(bool isPartial)
        {
            // Arrange
            var viewEngine = CreateSuccessfulViewEngine();
            var context = GetActionContext(_controllerTestContext);

            // Act
            var result = viewEngine.FindPage(context, "View.cshtml", isPartial);

            // Assert
            Assert.Null(result.Page);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GetPage_WithViewName_ReturnsNotFound(bool isPartial)
        {
            // Arrange
            var viewEngine = CreateSuccessfulViewEngine();

            // Act
            var result = viewEngine.GetPage("~/Home/View1.cshtml", "View2", isPartial);

            // Assert
            Assert.Null(result.Page);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void FindPage_IsPartial_ThrowsIfNameIsNullOrEmpty(string pageName)
        {
            // Arrange
            var viewEngine = CreateViewEngine();
            var context = GetActionContext(_controllerTestContext);

            // Act & Assert
            ExceptionAssert.ThrowsArgumentNullOrEmpty(
                () => viewEngine.FindPage(context, pageName, isPartial: true),
                "pageName");
        }

        [Theory]
        [MemberData(nameof(ViewLocationExpanderTestData))]
        public void FindPage_IsPartial_UsesViewLocationExpander_ToExpandPaths(
            IDictionary<string, object> routeValues,
            IEnumerable<string> expectedSeeds)
        {
            // Arrange
            var page = Mock.Of<IRazorPage>();
            var pageFactory = new Mock<IRazorPageFactory>();
            pageFactory
                .Setup(p => p.CreateInstance("expanded-path/bar-layout"))
                .Returns(page)
                .Verifiable();

            var viewFactory = new Mock<IRazorViewFactory>(MockBehavior.Strict);

            var expander = new Mock<IViewLocationExpander>();
            expander
                .Setup(e => e.PopulateValues(It.IsAny<ViewLocationExpanderContext>()))
                .Callback((ViewLocationExpanderContext c) =>
                {
                    Assert.NotNull(c.ActionContext);
                    c.Values["expander-key"] = expander.ToString();
                })
                .Verifiable();
            expander
                .Setup(e => e.ExpandViewLocations(
                    It.IsAny<ViewLocationExpanderContext>(),
                    It.IsAny<IEnumerable<string>>()))
                .Returns((ViewLocationExpanderContext c, IEnumerable<string> seeds) =>
                {
                    Assert.NotNull(c.ActionContext);
                    Assert.Equal(expectedSeeds, seeds);

                    Assert.Equal(expander.ToString(), c.Values["expander-key"]);

                    return new[] { "expanded-path/bar-{0}" };
                })
                .Verifiable();

            var viewEngine = CreateViewEngine(
                pageFactory.Object,
                viewFactory.Object,
                new[] { expander.Object });
            var context = GetActionContext(routeValues);

            // Act
            var result = viewEngine.FindPage(context, "layout", isPartial: true);

            // Assert
            Assert.Equal("layout", result.Name);
            Assert.Same(page, result.Page);
            Assert.Null(result.SearchedLocations);
            pageFactory.Verify();
            expander.Verify();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void FindPage_ReturnsSearchedLocationsIfPageCannotBeFound(bool isPartial)
        {
            // Arrange
            var expected = new[]
            {
                "/Views/bar/layout.cshtml",
                "/Views/Shared/layout.cshtml",
            };

            var viewEngine = CreateViewEngine();
            var context = GetActionContext(_controllerTestContext);

            // Act
            var result = viewEngine.FindPage(context, "layout", isPartial);

            // Assert
            Assert.Equal("layout", result.Name);
            Assert.Null(result.Page);
            Assert.Equal(expected, result.SearchedLocations);
        }

        [Theory]
        // Looks in RouteValueDefaults
        [InlineData(true)]
        // Looks in RouteConstraints
        [InlineData(false)]
        public void FindPage_IsPartial_SelectsActionCaseInsensitively(bool isAttributeRouted)
        {
            // The ActionDescriptor contains "Foo" and the RouteData contains "foo"
            // which matches the case of the constructor thus searching in the appropriate location.
            // Arrange
            var routeValues = new Dictionary<string, object>
            {
                { "controller", "foo" }
            };

            var page = new Mock<IRazorPage>(MockBehavior.Strict);
            page.SetupSet(p => p.IsPartial = true);

            var pageFactory = new Mock<IRazorPageFactory>();
            pageFactory
                .Setup(p => p.CreateInstance("/Views/Foo/details.cshtml"))
                .Returns(page.Object)
                .Verifiable();

            var viewEngine = CreateViewEngine(pageFactory.Object);
            var routesInActionDescriptor = new Dictionary<string, string>()
            {
                { "controller", "Foo" }
            };

            var context = GetActionContextWithActionDescriptor(
                routeValues,
                routesInActionDescriptor,
                isAttributeRouted);

            // Act
            var result = viewEngine.FindPage(context, "details", isPartial: true);

            // Assert
            Assert.Equal("details", result.Name);
            Assert.Same(page.Object, result.Page);
            Assert.Null(result.SearchedLocations);
            pageFactory.Verify();
        }

        [Theory]
        // Looks in RouteValueDefaults
        [InlineData(true)]
        // Looks in RouteConstraints
        [InlineData(false)]
        public void FindPage_IsPartial_LooksForPages_UsingActionDescriptor_Controller(bool isAttributeRouted)
        {
            // Arrange
            var expected = new[]
            {
                "/Views/bar/foo.cshtml",
                "/Views/Shared/foo.cshtml",
            };

            var routeValues = new Dictionary<string, object>
            {
                { "controller", "Bar" }
            };
            var routesInActionDescriptor = new Dictionary<string, string>()
            {
                { "controller", "bar" }
            };

            var viewEngine = CreateViewEngine();
            var context = GetActionContextWithActionDescriptor(
                routeValues,
                routesInActionDescriptor,
                isAttributeRouted);

            // Act
            var result = viewEngine.FindPage(context, "foo", isPartial: true);

            // Assert
            Assert.Equal("foo", result.Name);
            Assert.Null(result.Page);
            Assert.Equal(expected, result.SearchedLocations);
        }

        [Theory]
        // Looks in RouteValueDefaults
        [InlineData(true)]
        // Looks in RouteConstraints
        [InlineData(false)]
        public void FindPage_IsPartial_LooksForPages_UsingActionDescriptor_Areas(bool isAttributeRouted)
        {
            // Arrange
            var expected = new[]
            {
                "/Areas/world/Views/bar/foo.cshtml",
                "/Areas/world/Views/Shared/foo.cshtml",
                "/Views/Shared/foo.cshtml"
            };

            var routeValues = new Dictionary<string, object>
            {
                { "controller", "Bar" },
                { "area", "World" }
            };
            var routesInActionDescriptor = new Dictionary<string, string>()
            {
                { "controller", "bar" },
                { "area", "world" }
            };

            var viewEngine = CreateViewEngine();
            var context = GetActionContextWithActionDescriptor(
                routeValues,
                routesInActionDescriptor,
                isAttributeRouted);

            // Act
            var result = viewEngine.FindPage(context, "foo", isPartial: true);

            // Assert
            Assert.Equal("foo", result.Name);
            Assert.Null(result.Page);
            Assert.Equal(expected, result.SearchedLocations);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void FindPage_IsPartial_LooksForPages_UsesRouteValuesAsFallback(bool isAttributeRouted)
        {
            // Arrange
            var expected = new[]
            {
                "/Views/foo/bar.cshtml",
                "/Views/Shared/bar.cshtml",
            };

            var routeValues = new Dictionary<string, object>()
            {
                { "controller", "foo" }
            };

            var viewEngine = CreateViewEngine();
            var context = GetActionContextWithActionDescriptor(
                routeValues,
                new Dictionary<string, string>(),
                isAttributeRouted);

            // Act
            var result = viewEngine.FindPage(context, "bar", isPartial: true);

            // Assert
            Assert.Equal("bar", result.Name);
            Assert.Null(result.Page);
            Assert.Equal(expected, result.SearchedLocations);
        }

        [Theory]
        [InlineData("/Test-View.cshtml")]
        [InlineData("~/Test-View.CSHTML")]
        [InlineData("/Home/Test-View.CSHTML")]
        [InlineData("~/Home/Test-View.cshtml")]
        [InlineData("~/SHARED/TEST-VIEW.CSHTML")]
        public void GetPage_UsesGivenPath_WithAppRelativePath(string pageName)
        {
            // Arrange
            var pageFactory = new Mock<IRazorPageFactory>();
            var page = Mock.Of<IRazorPage>();
            pageFactory
                .Setup(p => p.CreateInstance(pageName))
                .Returns(page)
                .Verifiable();

            var viewFactory = new Mock<IRazorViewFactory>(MockBehavior.Strict);
            var viewEngine = new OverloadedLocationViewEngine(
                pageFactory.Object,
                viewFactory.Object,
                GetOptionsAccessor(),
                GetViewLocationCache());

            // Act
            var result = viewEngine.GetPage("~/Another/Place.cshtml", pagePath: pageName, isPartial: false);

            // Assert
            Assert.Same(page, result.Page);
            Assert.Equal(pageName, result.Name);
            pageFactory.Verify();
        }

        [Theory]
        [InlineData("Test-View.cshtml")]
        [InlineData("Test-View.CSHTML")]
        [InlineData("PATH/TEST-VIEW.CSHTML")]
        [InlineData("Path1/Path2/Test-View.cshtml")]
        public void GetPage_ResolvesRelativeToCurrentPage_WithRelativePath(string pageName)
        {
            // Arrange
            var expectedPageName = $"/Home/{ pageName }";
            var pageFactory = new Mock<IRazorPageFactory>();
            var page = Mock.Of<IRazorPage>();
            pageFactory
                .Setup(p => p.CreateInstance(expectedPageName))
                .Returns(page)
                .Verifiable();

            var viewFactory = new Mock<IRazorViewFactory>(MockBehavior.Strict);
            var viewEngine = new OverloadedLocationViewEngine(
                pageFactory.Object,
                viewFactory.Object,
                GetOptionsAccessor(),
                GetViewLocationCache());

            // Act
            var result = viewEngine.GetPage("/Home/Page.cshtml", pageName, isPartial: false);

            // Assert
            Assert.Same(page, result.Page);
            Assert.Equal(expectedPageName, result.Name);
            pageFactory.Verify();
        }

        [Theory]
        [InlineData("Test-View.cshtml")]
        [InlineData("Test-View.CSHTML")]
        [InlineData("PATH/TEST-VIEW.CSHTML")]
        [InlineData("Path1/Path2/Test-View.cshtml")]
        public void GetPage_ResolvesRelativeToAppRoot_WithRelativePath_IfNoPageExecuting(string pageName)
        {
            // Arrange
            var expectedPageName = $"/{ pageName }";
            var pageFactory = new Mock<IRazorPageFactory>();
            var page = Mock.Of<IRazorPage>();
            pageFactory
                .Setup(p => p.CreateInstance(expectedPageName))
                .Returns(page)
                .Verifiable();

            var viewFactory = new Mock<IRazorViewFactory>(MockBehavior.Strict);
            var viewEngine = new OverloadedLocationViewEngine(
                pageFactory.Object,
                viewFactory.Object,
                GetOptionsAccessor(),
                GetViewLocationCache());

            // Act
            var result = viewEngine.GetPage(executingFilePath: null, pagePath: pageName, isPartial: false);

            // Assert
            Assert.Same(page, result.Page);
            Assert.Equal(expectedPageName, result.Name);
            pageFactory.Verify();
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData(null, "")]
        [InlineData(null, "Page")]
        [InlineData(null, "Folder/Page")]
        [InlineData(null, "Folder1/Folder2/Page")]
        [InlineData("/Home/Index.cshtml", null)]
        [InlineData("/Home/Index.cshtml", "")]
        [InlineData("/Home/Index.cshtml", "Page")]
        [InlineData("/Home/Index.cshtml", "Folder/Page")]
        [InlineData("/Home/Index.cshtml", "Folder1/Folder2/Page")]
        public void MakePathAbsolute_ReturnsPagePathUnchanged_IfNotAPath(string executingFilePath, string pagePath)
        {
            // Arrange
            var viewEngine = CreateViewEngine();

            // Act
            var result = viewEngine.MakePathAbsolute(executingFilePath, pagePath);

            // Assert
            Assert.Same(pagePath, result);
        }

        [Theory]
        [InlineData(null, "/Page")]
        [InlineData(null, "~/Folder/Page.cshtml")]
        [InlineData(null, "/Folder1/Folder2/Page.rzr")]
        [InlineData("/Home/Index.cshtml", "~/Page")]
        [InlineData("/Home/Index.cshtml", "/Folder/Page.cshtml")]
        [InlineData("/Home/Index.cshtml", "~/Folder1/Folder2/Page.rzr")]
        public void MakePathAbsolute_ReturnsPageUnchanged_IfAppRelative(string executingFilePath, string pagePath)
        {
            // Arrange
            var viewEngine = CreateViewEngine();

            // Act
            var result = viewEngine.MakePathAbsolute(executingFilePath, pagePath);

            // Assert
            Assert.Same(pagePath, result);
        }

        [Theory]
        [InlineData("Page.cshtml")]
        [InlineData("Folder/Page.cshtml")]
        [InlineData("../../Folder1/Folder2/Page.cshtml")]
        public void MakePathAbsolute_ResolvesRelativeToExecutingPage(string pagePath)
        {
            // Arrange
            var expectedPagePath = "/Home/" + pagePath;
            var viewEngine = CreateViewEngine();

            // Act
            var result = viewEngine.MakePathAbsolute("/Home/Page.cshtml", pagePath);

            // Assert
            Assert.Equal(expectedPagePath, result);
        }

        [Theory]
        [InlineData("Page.cshtml")]
        [InlineData("Folder/Page.cshtml")]
        [InlineData("../../Folder1/Folder2/Page.cshtml")]
        public void MakePathAbsolute_ResolvesRelativeToAppRoot_IfNoPageExecuting(string pagePath)
        {
            // Arrange
            var expectedPagePath = "/" + pagePath;
            var viewEngine = CreateViewEngine();

            // Act
            var result = viewEngine.MakePathAbsolute(executingFilePath: null, pagePath: pagePath);

            // Assert
            Assert.Equal(expectedPagePath, result);
        }

        [Fact]
        public void AreaViewLocationFormats_ContainsExpectedLocations()
        {
            // Arrange
            var viewEngine = CreateViewEngine();
            var areaViewLocations = new string[]
            {
                "/Areas/{2}/Views/{1}/{0}.cshtml",
                "/Areas/{2}/Views/Shared/{0}.cshtml",
                "/Views/Shared/{0}.cshtml"
            };

            // Act & Assert
            Assert.Equal(areaViewLocations, viewEngine.AreaViewLocationFormats);
        }

        [Fact]
        public void ViewLocationFormats_ContainsExpectedLocations()
        {
            // Arrange
            var viewEngine = CreateViewEngine();

            var viewLocations = new string[]
            {
                "/Views/{1}/{0}.cshtml",
                "/Views/Shared/{0}.cshtml"
            };

            // Act & Assert
            Assert.Equal(viewLocations, viewEngine.ViewLocationFormats);
        }

        [Fact]
        public void GetNormalizedRouteValue_ReturnsValueFromRouteConstraints_IfKeyHandlingIsRequired()
        {
            // Arrange
            var key = "some-key";
            var actionDescriptor = new ActionDescriptor
            {
                RouteConstraints = new[]
                {
                    new RouteDataActionConstraint(key, "Route-Value")
                }
            };

            var actionContext = new ActionContext
            {
                ActionDescriptor = actionDescriptor,
                RouteData = new RouteData()
            };

            actionContext.RouteData.Values[key] = "route-value";

            // Act
            var result = RazorViewEngine.GetNormalizedRouteValue(actionContext, key);

            // Assert
            Assert.Equal("Route-Value", result);
        }

        [Fact]
        public void GetNormalizedRouteValue_ReturnsRouteValue_IfValueDoesNotMatchRouteConstraint()
        {
            // Arrange
            var key = "some-key";
            var actionDescriptor = new ActionDescriptor
            {
                RouteConstraints = new[]
                {
                    new RouteDataActionConstraint(key, "different-value")
                }
            };

            var actionContext = new ActionContext
            {
                ActionDescriptor = actionDescriptor,
                RouteData = new RouteData()
            };

            actionContext.RouteData.Values[key] = "route-value";

            // Act
            var result = RazorViewEngine.GetNormalizedRouteValue(actionContext, key);

            // Assert
            Assert.Equal("route-value", result);
        }

        [Fact]
        public void GetNormalizedRouteValue_ReturnsNull_IfRouteConstraintKeyHandlingIsDeny()
        {
            // Arrange
            var key = "some-key";
            var actionDescriptor = new ActionDescriptor
            {
                RouteConstraints = new[]
                {
                    new RouteDataActionConstraint(key, routeValue: string.Empty)
                }
            };

            var actionContext = new ActionContext
            {
                ActionDescriptor = actionDescriptor,
                RouteData = new RouteData()
            };

            actionContext.RouteData.Values[key] = "route-value";

            // Act
            var result = RazorViewEngine.GetNormalizedRouteValue(actionContext, key);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetNormalizedRouteValue_ReturnsRouteDataValue_IfRouteConstraintKeyHandlingIsCatchAll()
        {
            // Arrange
            var key = "some-key";
            var actionDescriptor = new ActionDescriptor
            {
                RouteConstraints = new[]
                {
                    RouteDataActionConstraint.CreateCatchAll(key)
                }
            };

            var actionContext = new ActionContext
            {
                ActionDescriptor = actionDescriptor,
                RouteData = new RouteData()
            };

            actionContext.RouteData.Values[key] = "route-value";

            // Act
            var result = RazorViewEngine.GetNormalizedRouteValue(actionContext, key);

            // Assert
            Assert.Equal("route-value", result);
        }

        [Fact]
        public void GetNormalizedRouteValue_UsesRouteValueDefaults_IfAttributeRouted()
        {
            // Arrange
            var key = "some-key";
            var actionDescriptor = new ActionDescriptor
            {
                AttributeRouteInfo = new AttributeRouteInfo(),
            };
            actionDescriptor.RouteValueDefaults[key] = "Route-Value";

            var actionContext = new ActionContext
            {
                ActionDescriptor = actionDescriptor,
                RouteData = new RouteData()
            };

            actionContext.RouteData.Values[key] = "route-value";

            // Act
            var result = RazorViewEngine.GetNormalizedRouteValue(actionContext, key);

            // Assert
            Assert.Equal("Route-Value", result);
        }

        [Fact]
        public void GetNormalizedRouteValue_UsesRouteValue_IfRouteValueDefaultsDoesNotMatchRouteValue()
        {
            // Arrange
            var key = "some-key";
            var actionDescriptor = new ActionDescriptor
            {
                AttributeRouteInfo = new AttributeRouteInfo(),
            };
            actionDescriptor.RouteValueDefaults[key] = "different-value";

            var actionContext = new ActionContext
            {
                ActionDescriptor = actionDescriptor,
                RouteData = new RouteData()
            };

            actionContext.RouteData.Values[key] = "route-value";

            // Act
            var result = RazorViewEngine.GetNormalizedRouteValue(actionContext, key);

            // Assert
            Assert.Equal("route-value", result);
        }

        [Fact]
        public void GetNormalizedRouteValue_ConvertsRouteDefaultToStringValue_IfAttributeRouted()
        {
            using (new CultureReplacer())
            {
                // Arrange
                var key = "some-key";
                var actionDescriptor = new ActionDescriptor
                {
                    AttributeRouteInfo = new AttributeRouteInfo(),
                };
                actionDescriptor.RouteValueDefaults[key] = 32;

                var actionContext = new ActionContext
                {
                    ActionDescriptor = actionDescriptor,
                    RouteData = new RouteData()
                };

                actionContext.RouteData.Values[key] = 32;

                // Act
                var result = RazorViewEngine.GetNormalizedRouteValue(actionContext, key);

                // Assert
                Assert.Equal("32", result);
            }
        }

        [Fact]
        public void GetNormalizedRouteValue_UsesRouteDataValue_IfKeyDoesNotExistInRouteDefaultValues()
        {
            // Arrange
            var key = "some-key";
            var actionDescriptor = new ActionDescriptor
            {
                AttributeRouteInfo = new AttributeRouteInfo(),
            };

            var actionContext = new ActionContext
            {
                ActionDescriptor = actionDescriptor,
                RouteData = new RouteData()
            };

            actionContext.RouteData.Values[key] = "route-value";

            // Act
            var result = RazorViewEngine.GetNormalizedRouteValue(actionContext, key);

            // Assert
            Assert.Equal("route-value", result);
        }

        [Fact]
        public void GetNormalizedRouteValue_ConvertsRouteValueToString()
        {
            using (new CultureReplacer())
            {
                // Arrange
                var key = "some-key";
                var actionDescriptor = new ActionDescriptor
                {
                    AttributeRouteInfo = new AttributeRouteInfo(),
                };

                var actionContext = new ActionContext
                {
                    ActionDescriptor = actionDescriptor,
                    RouteData = new RouteData()
                };

                actionContext.RouteData.Values[key] = 43;

                // Act
                var result = RazorViewEngine.GetNormalizedRouteValue(actionContext, key);

                // Assert
                Assert.Equal("43", result);
            }
        }

        // Return RazorViewEngine with factories that always successfully create instances.
        private RazorViewEngine CreateSuccessfulViewEngine()
        {
            var pageFactory = new Mock<IRazorPageFactory>(MockBehavior.Strict);
            pageFactory
                .Setup(f => f.CreateInstance(It.IsAny<string>()))
                .Returns(Mock.Of<IRazorPage>());
            var viewFactory = new Mock<IRazorViewFactory>(MockBehavior.Strict);
            viewFactory
                .Setup(f => f.GetView(It.IsAny<IRazorViewEngine>(), It.IsAny<IRazorPage>(), /*isPartial*/ false))
                .Returns(Mock.Of<IView>());

            return CreateViewEngine(pageFactory.Object, viewFactory.Object);
        }

        private RazorViewEngine CreateViewEngine(
            IRazorPageFactory pageFactory = null,
            IRazorViewFactory viewFactory = null,
            IEnumerable<IViewLocationExpander> expanders = null,
            IViewLocationCache cache = null)
        {
            pageFactory = pageFactory ?? Mock.Of<IRazorPageFactory>();
            viewFactory = viewFactory ?? Mock.Of<IRazorViewFactory>();

            cache = cache ?? GetViewLocationCache();

            var viewEngine = new RazorViewEngine(
                pageFactory,
                viewFactory,
                GetOptionsAccessor(expanders),
                cache);

            return viewEngine;
        }

        private static IOptions<RazorViewEngineOptions> GetOptionsAccessor(
            IEnumerable<IViewLocationExpander> expanders = null)
        {
            var options = new RazorViewEngineOptions();
            if (expanders != null)
            {
                foreach (var expander in expanders)
                {
                    options.ViewLocationExpanders.Add(expander);
                }
            }

            var optionsAccessor = new Mock<IOptions<RazorViewEngineOptions>>();
            optionsAccessor
                .SetupGet(v => v.Value)
                .Returns(options);
            return optionsAccessor.Object;
        }

        private static IViewLocationCache GetViewLocationCache()
        {
            var cacheMock = new Mock<IViewLocationCache>();
            cacheMock
                .Setup(c => c.Get(It.IsAny<ViewLocationExpanderContext>()))
                .Returns<string>(null);

            return cacheMock.Object;
        }

        private static ActionContext GetActionContext(IDictionary<string, object> routeValues)
        {
            var httpContext = new DefaultHttpContext();
            var routeData = new RouteData();
            foreach (var kvp in routeValues)
            {
                routeData.Values.Add(kvp.Key, kvp.Value);
            }

            var actionDesciptor = new ActionDescriptor();
            actionDesciptor.RouteConstraints = new List<RouteDataActionConstraint>();
            return new ActionContext(httpContext, routeData, actionDesciptor);
        }

        private static ActionContext GetActionContextWithActionDescriptor(
            IDictionary<string, object> routeValues,
            IDictionary<string, string> routesInActionDescriptor,
            bool isAttributeRouted)
        {
            var httpContext = new DefaultHttpContext();
            var routeData = new RouteData();
            foreach (var kvp in routeValues)
            {
                routeData.Values.Add(kvp.Key, kvp.Value);
            }

            var actionDescriptor = new ActionDescriptor();
            if (isAttributeRouted)
            {
                actionDescriptor.AttributeRouteInfo = new AttributeRouteInfo();
                foreach (var kvp in routesInActionDescriptor)
                {
                    actionDescriptor.RouteValueDefaults.Add(kvp.Key, kvp.Value);
                }
            }
            else
            {
                actionDescriptor.RouteConstraints = new List<RouteDataActionConstraint>();
                foreach (var kvp in routesInActionDescriptor)
                {
                    actionDescriptor.RouteConstraints.Add(new RouteDataActionConstraint(kvp.Key, kvp.Value));
                }
            }

            return new ActionContext(httpContext, routeData, actionDescriptor);
        }

        private class OverloadedLocationViewEngine : RazorViewEngine
        {
            public OverloadedLocationViewEngine(
                IRazorPageFactory pageFactory,
                IRazorViewFactory viewFactory,
                IOptions<RazorViewEngineOptions> optionsAccessor,
                IViewLocationCache cache)
                : base(pageFactory, viewFactory, optionsAccessor, cache)
            {
            }

            public override IEnumerable<string> ViewLocationFormats
            {
                get
                {
                    return new[] { "fake-path1/{1}/{0}.rzr" };
                }
            }

            public override IEnumerable<string> AreaViewLocationFormats
            {
                get
                {
                    return new[] { "fake-area-path/{2}/{1}/{0}.rzr" };
                }
            }
        }
    }
}
