// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNet.Mvc.DataAnnotations.Internal;
using Microsoft.AspNet.Mvc.Formatters.Json.Internal;
using Microsoft.AspNet.Mvc.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.OptionsModel;

namespace Microsoft.AspNet.Mvc.IntegrationTests
{
    public class TestMvcOptions : IOptions<MvcOptions>
    {
        public TestMvcOptions()
        {
            Value = new MvcOptions();
            MvcCoreMvcOptionsSetup.ConfigureMvc(Value, new TestHttpRequestStreamReaderFactory());
            var collection = new ServiceCollection().AddOptions();
            MvcDataAnnotationsMvcOptionsSetup.ConfigureMvc(
                Value,
                collection.BuildServiceProvider());
            MvcJsonMvcOptionsSetup.ConfigureMvc(Value, SerializerSettingsProvider.CreateSerializerSettings());
        }

        public MvcOptions Value { get; }
    }
}