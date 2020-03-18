// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using FluentAssertions;
using Microsoft.CodeAnalysis.Test.Utilities.Sarif;
using Xunit;

namespace Microsoft.CodeAnalysis.Sarif.WorkItems
{
    public class SarifWorkItemFilingContextTests
    {
        [Fact]
        public void WorkItemFilingContext_RoundTripsWorkItemModelTransformer()
        {
            var munger = new Munger();

            var context = new SarifWorkItemContext();

            context.AddWorkItemModelTransformer(munger);
            context.Transformers[0].GetType().Should().Be(munger.GetType());

            context = RoundTripThroughXml(context);

            context.Transformers[0].GetType().Should().Be(munger.GetType());

            string newAreaPath = Guid.NewGuid().ToString();
            context.SetProperty(Munger.NewAreaPath, newAreaPath);

            var workItemModel = new SarifWorkItemModel(sarifLog: null, context);

            context.Transformers[0].Transform(workItemModel);
            workItemModel.Area.Should().Be(newAreaPath);
        }

        [Fact]
        public void WorkItemFilingContext_NullAreaPathRemainsUnchanged()
        {
            var areaPathTransformer = new AreaPathFromUri();

            var context = new SarifWorkItemContext();

            context.AddWorkItemModelTransformer(areaPathTransformer);
            context.Transformers[0].GetType().Should().Be(areaPathTransformer.GetType());

            var workItemModel = new SarifWorkItemModel(sarifLog: null, context);

            context.Transformers[0].Transform(workItemModel);
            workItemModel.Area.Should().BeNull();
        }

        [Fact]
        public void WorkItemFilingContext_FetchUriSuccessfully()
        {
            var areaPathTransformer = new AreaPathFromUri();

            var context = new SarifWorkItemContext();

            context.AddWorkItemModelTransformer(areaPathTransformer);
            context.Transformers[0].GetType().Should().Be(areaPathTransformer.GetType());

            SarifLog sarifLog = TestConstants.SarifLogs.OneIdThreeLocations;

            var workItemModel = new SarifWorkItemModel(sarifLog, context);

            context.Transformers[0].Transform(workItemModel);
            workItemModel.Area.Should().Be(TestConstants.FileLocations.Location1);
        }

        [Fact]
        public void WorkItemFilingContext_CompCentralTransformer()
        {
            ResourceExtractor extractor = new ResourceExtractor(typeof(SarifWorkItemFilingContextTests));
            string configXml = extractor.GetResourceText("TestWorkItemContext.xml");
            SarifWorkItemContext context = LoadFromXml(configXml);
            SarifLog sarifLog = TestConstants.SarifLogs.OneIdThreeLocations;

            var workItemModel = new SarifWorkItemModel(sarifLog, context);

            context.Transformers[0].Transform(workItemModel);
            workItemModel.Area.Should().Be(TestConstants.FileLocations.Location1);
        }

        private SarifWorkItemContext RoundTripThroughXml(SarifWorkItemContext sarifWorkItemContext)
        {
            string temp = Path.GetTempFileName();

            try
            {
                sarifWorkItemContext.SaveToXml(temp);
                sarifWorkItemContext = new SarifWorkItemContext();
                sarifWorkItemContext.LoadFromXml(temp);
            }
            finally
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
            return sarifWorkItemContext;
        }

        private SarifWorkItemContext LoadFromXml(string contextInformation)
        {
            var sarifWorkItemContext = new SarifWorkItemContext();
            byte[] byteArray = Encoding.UTF8.GetBytes(contextInformation);
            MemoryStream stream = new MemoryStream(byteArray);
            sarifWorkItemContext.LoadFromXml(stream);

            return sarifWorkItemContext;
        }


        public class Munger : SarifWorkItemModelTransformer
        {
            public override void Transform(SarifWorkItemModel workItemModel)
            {
                string newAreaPath = workItemModel.Context.GetProperty(NewAreaPath);

                workItemModel.Area = !string.IsNullOrEmpty(newAreaPath) ? newAreaPath : workItemModel.Area;
            }

            public static PerLanguageOption<string> NewAreaPath { get; } =
                new PerLanguageOption<string>(
                    nameof(Munger), nameof(NewAreaPath),
                    defaultValue: () => { return null; });
        }

        public class AreaPathFromUri : SarifWorkItemModelTransformer
        {
            public override void Transform(SarifWorkItemModel workItemModel)
            {
                string newAreaPath = workItemModel.LocationUri?.OriginalString;

                workItemModel.Area = !string.IsNullOrEmpty(newAreaPath) ? newAreaPath : workItemModel.Area;
            }
        }
    }
}
