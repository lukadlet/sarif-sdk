// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.CodeAnalysis.Test.Utilities.Sarif;
using Xunit;

namespace Microsoft.CodeAnalysis.Sarif.Visitors
{
    public class PerRunPerTargetPerRuleSplittingVisitorTests
    {
        [Fact]
        public void PerRunPerRuleSplittingVisitor_EmptyLog()
        {
            var visitor = new PerRunPerLocationPerRuleSplittingVisitor();
            visitor.VisitRun(new Run());
            visitor.SplitSarifLogs.Count.Should().Be(0);
        }

        [Fact]
        public void PerRunPerRuleSplittingVisitor_SplitsMultipleLocationsAcrossRuns()
        {
            var sarifLog = new SarifLog
            {
                Runs = new[]
                {
                    new Run
                    {
                        Results = new[]
                        {
                            new Result
                            {
                                RuleId = TestConstants.RuleIds.Rule1,
                                BaselineState = BaselineState.New,
                                Locations = new List<Location>()
                                {
                                    new Location()
                                    {
                                        PhysicalLocation = new PhysicalLocation()
                                        {
                                            ArtifactLocation = new ArtifactLocation
                                            {
                                                Uri = new Uri("C:\\sarif.txt")
                                            }
                                        }
                                    }
                                }
                            },

                            new Result
                            {
                                RuleId = TestConstants.RuleIds.Rule2,
                                BaselineState = BaselineState.New,
                                Locations = new List<Location>()
                                {
                                    new Location()
                                    {
                                        PhysicalLocation = new PhysicalLocation()
                                        {
                                            ArtifactLocation = new ArtifactLocation
                                            {
                                                Uri = new Uri("C:\\sarif1.txt")
                                            }
                                        }
                                    },
                                    new Location()
                                    {
                                        PhysicalLocation = new PhysicalLocation()
                                        {
                                            ArtifactLocation = new ArtifactLocation
                                            {
                                                Uri = new Uri("C:\\sarif2.txt")
                                            }
                                        }
                                    }
                                }

                            },

                            new Result
                            {
                                RuleId = TestConstants.RuleIds.Rule2,
                                BaselineState = BaselineState.New,
                                Locations = new List<Location>()
                                {
                                    new Location()
                                    {
                                        PhysicalLocation = new PhysicalLocation()
                                        {
                                            ArtifactLocation = new ArtifactLocation
                                            {
                                                Uri = new Uri("C:\\sarif2.txt")
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            var visitor = new PerRunPerTargetPerRuleSplittingVisitor();
            visitor.VisitSarifLog(sarifLog);

            visitor.SplitSarifLogs.Count.Should().Be(3);

            visitor.SplitSarifLogs[0].Runs[0].Results.Count.Should().Be(1);
            visitor.SplitSarifLogs[0].Runs[0].Results[0].RuleId.Should().Be(TestConstants.RuleIds.Rule1);
            visitor.SplitSarifLogs[0].Runs[0].Results[0].Locations[0].PhysicalLocation.ArtifactLocation.Uri.Should().Be("C:\\sarif.txt");

            visitor.SplitSarifLogs[1].Runs[0].Results.Count.Should().Be(1);
            visitor.SplitSarifLogs[1].Runs[0].Results[0].RuleId.Should().Be(TestConstants.RuleIds.Rule2);
            visitor.SplitSarifLogs[1].Runs[0].Results[0].Locations[0].PhysicalLocation.ArtifactLocation.Uri.Should().Be("C:\\sarif1.txt");

            visitor.SplitSarifLogs[2].Runs[0].Results.Count.Should().Be(1);
            visitor.SplitSarifLogs[2].Runs[0].Results[0].RuleId.Should().Be(TestConstants.RuleIds.Rule2);
            visitor.SplitSarifLogs[2].Runs[0].Results[0].Locations[0].PhysicalLocation.ArtifactLocation.Uri.Should().Be("C:\\sarif2.txt");
        }


        [Fact]
        public void WorkItemFiler_ValidatesPerRunPerRuleSplittingStrategy()
        {
            /* var filer = CreateWorkItemFiler();
             filer.FilingClient = new TestFilingClient();
             filer.FilingContext = new SarifWorkItemContext();
             filer.FilingContext.SplittingStrategy = SplittingStrategy.PerRunPerRule;

             ResourceExtractor ResourceExtractor = new ResourceExtractor(typeof(SarifWorkItemFilerTests));
             string sarifData = ResourceExtractor.GetResourceText("Sarif.WorkItems.TestData.NewAndOldResults.sarif");

             Action action = () => filer.FileWorkItems(sarifLogFileContents: sarifData);

             action.Should().Throw<ArgumentNullException>();*/
        }
    }
}
