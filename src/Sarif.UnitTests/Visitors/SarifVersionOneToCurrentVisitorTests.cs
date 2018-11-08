﻿// Copyright (c) Microsoft. All rights reserved. Licensed under the MIT        
// license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Text;
using FluentAssertions;
using Microsoft.CodeAnalysis.Sarif.VersionOne;
using Microsoft.CodeAnalysis.Sarif.Visitors;
using Microsoft.CodeAnalysis.Sarif.Writers;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.CodeAnalysis.Sarif.UnitTests.Transformers
{
    public class SarifVersionOneToCurrentVisitorTests : FileDiffingTests
    {
        public SarifVersionOneToCurrentVisitorTests(ITestOutputHelper outputHelper) : base(outputHelper) { }


        private static SarifLogVersionOne GetSarifLogVersionOne(string logText)
        {
            return JsonConvert.DeserializeObject<SarifLogVersionOne>(logText, SarifTransformerUtilities.JsonSettingsV1Indented);
        }
        
        private static SarifLog TransformVersionOneToCurrent(string v1LogText)
        {
            SarifLogVersionOne v1Log = GetSarifLogVersionOne(v1LogText);
            var transformer = new SarifVersionOneToCurrentVisitor();
            transformer.VisitSarifLogVersionOne(v1Log);

            return transformer.SarifLog;
        }

        private static void VerifyVersionOneToCurrentTransformation(string v1LogText, string v2LogExpectedText)
        {
            SarifLog v2Log = TransformVersionOneToCurrent(v1LogText);
            string v2LogText = JsonConvert.SerializeObject(v2Log, SarifTransformerUtilities.JsonSettingsIndented);
            v2LogText.Should().Be(v2LogExpectedText);
        }

        private static bool s_Rebaseline = false;

        private void VerifyVersionOneToCurrentTransformationFromResource(string v1InputResourceName, string v2ExpectedResourceName = null)
        {
            v2ExpectedResourceName = v2ExpectedResourceName ?? v1InputResourceName;

            string v1LogText = GetResourceText($"v1.{v1InputResourceName}");
            string v2ExpectedLogText = GetResourceText($"v2.{v2ExpectedResourceName}");

            v2ExpectedLogText = PrereleaseCompatibilityTransformer.UpdateToCurrentVersion(v2ExpectedLogText, forceUpdate: true, formatting: Formatting.Indented);

            SarifLog v2Log = TransformVersionOneToCurrent(v1LogText);
            string v2ActualLogText = JsonConvert.SerializeObject(v2Log, SarifTransformerUtilities.JsonSettingsIndented);

            StringBuilder sb = new StringBuilder();

            string expectedFilePath = GetOutputFilePath("expected", v2ExpectedResourceName);
            string actualFilePath = GetOutputFilePath("actual", v2ExpectedResourceName);

            string expectedRootDirectory = Path.GetDirectoryName(expectedFilePath);
            string actualRootDirectory = Path.GetDirectoryName(actualFilePath);

            if (!AreEquivalentSarifLogs<SarifLog>(v2ActualLogText, v2ExpectedLogText))
            {
                Directory.CreateDirectory(expectedRootDirectory);
                Directory.CreateDirectory(actualRootDirectory);

                File.WriteAllText(expectedFilePath, v2ExpectedLogText);
                File.WriteAllText(actualFilePath, v2ActualLogText);


                string errorMessage = string.Format(@"V2 conversion from V1 produced unexpected diffs for test: '{0}'.", v1InputResourceName);
                sb.AppendLine("Check individual differences with:");
                sb.AppendLine(GenerateDiffCommand(expectedFilePath, actualFilePath) + Environment.NewLine);

                sb.AppendLine("To compare all difference for this test suite:");
                sb.AppendLine(GenerateDiffCommand(Path.GetDirectoryName(expectedFilePath), Path.GetDirectoryName(actualFilePath)) + Environment.NewLine);
            }

            if (s_Rebaseline)
            {
                // We rewrite to test output directory. This allows subsequent tests to 
                // pass without requiring a rebuild that recopies SARIF test files
                File.WriteAllText(expectedFilePath, v2ActualLogText);

                string subdirectory = ProductTestDataDirectory;
                expectedFilePath = Path.Combine(ProductTestDataDirectory, "v2", Path.GetFileName(expectedFilePath));

                // We also rewrite the checked in test baselines
                File.WriteAllText(expectedFilePath, v2ActualLogText);

            }

            s_Rebaseline.Should().BeFalse();

            ValidateResults(sb.ToString());
        }

        [Fact]
        public void SarifTransformerTests_ToCurrent_RestoreFromPropertyBag()
        {
            VerifyVersionOneToCurrentTransformationFromResource("RestoreFromPropertyBag.sarif");
        }

        [Fact]
        public void SarifTransformerTests_ToCurrent_Minimum()
        {
            VerifyVersionOneToCurrentTransformationFromResource("Minimum.sarif");
        }

        [Fact]
        public void SarifTransformerTests_ToCurrent_MinimumWithTwoRuns()
        {
            VerifyVersionOneToCurrentTransformationFromResource("MinimumWithTwoRuns.sarif");
        }

        [Fact]
        public void SarifTransformerTests_ToCurrent_MinimumWithPropertyAndTags()
        {
            VerifyVersionOneToCurrentTransformationFromResource("MinimumWithPropertiesAndTags.sarif");
        }

        [Fact]
        public void SarifTransformerTests_ToCurrent_OneRunWithLogicalLocations()
        {
            VerifyVersionOneToCurrentTransformationFromResource("OneRunWithLogicalLocations.sarif");
        }

        [Fact]
        public void SarifTransformerTests_ToCurrent_OneRunWithFiles()
        {
            VerifyVersionOneToCurrentTransformationFromResource("OneRunWithFiles.sarif");
        }

        [Fact]
        public void SarifTransformerTests_ToCurrent_OneRunWitRules()
        {
            VerifyVersionOneToCurrentTransformationFromResource("OneRunWithRules.sarif");
        }

        [Fact]
        public void SarifTransformerTests_ToCurrent_OneRunWithBasicInvocation()
        {
            VerifyVersionOneToCurrentTransformationFromResource("OneRunWithBasicInvocation.sarif");
        }

        [Fact]
        public void SarifTransformerTests_ToCurrent_OneRunWithInvocationAndNotifications()
        {            
            VerifyVersionOneToCurrentTransformationFromResource("OneRunWithInvocationAndNotifications.sarif");
        }

        [Fact]
        public void SarifTransformerTests_ToCurrent_OneRunWithNotificationsButNoInvocations()
        {
            VerifyVersionOneToCurrentTransformationFromResource("OneRunWithNotificationsButNoInvocations.sarif");
        }

        [Fact]
        public void SarifTransformerTests_ToCurrent_NotificationExceptionWithStack()
        {
            VerifyVersionOneToCurrentTransformationFromResource("NotificationExceptionWithStack.sarif");
        }

        [Fact]
        public void SarifTransformerTests_ToCurrent_BasicResult()
        {
            VerifyVersionOneToCurrentTransformationFromResource("BasicResult.sarif");
        }

        [Fact]
        public void SarifTransformerTests_ToCurrent_TwoResultsWithFixes()
        {
            VerifyVersionOneToCurrentTransformationFromResource("TwoResultsWithFixes.sarif");
        }

        [Fact]
        public void SarifTransformerTests_ToCurrent_CodeFlows()
        {
            VerifyVersionOneToCurrentTransformationFromResource(v1InputResourceName: "CodeFlows.sarif",
                                                                v2ExpectedResourceName: "CodeFlows.sarif");
        }
    }
}
