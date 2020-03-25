﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Sarif.Visitors;
using Microsoft.WorkItems;
using Newtonsoft.Json;

namespace Microsoft.CodeAnalysis.Sarif.WorkItems
{
    public class SarifWorkItemFiler : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SarifWorkItemFiler"> class.</see>
        /// </summary>
        /// <param name="filingUri">
        /// The uri to the remote filing host.
        /// </param>
        /// <param name="filingContext">
        /// A starting context object that configures the work item filing operation. In the
        /// current implementation, this context is copied for each SARIF file (if any) split
        /// from the input log and then further elaborated upon.
        /// </param>
        public SarifWorkItemFiler(Uri filingUri = null, SarifWorkItemContext filingContext = null)
        {
            this.FilingContext = filingContext ?? new SarifWorkItemContext { HostUri = filingUri };
            filingUri = filingUri ?? this.FilingContext.HostUri;

            if (filingUri == null) { throw new ArgumentNullException(nameof(filingUri)); };

            if (filingUri != this.FilingContext.HostUri)
            {
                // Inconsistent URIs were provided in 'filingContext' and 'filingUri'; arguments.
                throw new InvalidOperationException(WorkItemsResources.InconsistentHostUrisProvided);
            }

            this.FilingClient = FilingClientFactory.Create(this.FilingContext.HostUri);
        }

        public FilingClient FilingClient { get; set; }

        public SarifWorkItemContext FilingContext { get; }

        public List<WorkItemModel> FiledWorkItems { get; private set; }

        public bool FilingSucceeded { get; private set; }

        public virtual void FileWorkItems(Uri sarifLogFileLocation)
        {
            sarifLogFileLocation = sarifLogFileLocation ?? throw new ArgumentNullException(nameof(sarifLogFileLocation));

            if (sarifLogFileLocation.IsAbsoluteUri && sarifLogFileLocation.Scheme == "file")
            {
                if (sarifLogFileLocation.IsAbsoluteUri && sarifLogFileLocation.Scheme == "file:")
                {
                    using (var stream = new FileStream(sarifLogFileLocation.LocalPath, FileMode.Open, FileAccess.Read))
                    using (var reader = new StreamReader(stream))
                    using (var jsonReader = new JsonTextReader(reader))
                    {
                        var serializer = new JsonSerializer();
                        SarifLog sarifLog = serializer.Deserialize<SarifLog>(jsonReader);
                        FileWorkItems(sarifLog);
                    }
                }
            }
            throw new ArgumentException($"Specified URI was not an absolute file URI: {sarifLogFileLocation}");
        }

        public virtual void FileWorkItems(string sarifLogFileContents, out SarifLog sarifLog)
        {
            sarifLogFileContents = sarifLogFileContents ?? throw new ArgumentNullException(nameof(sarifLogFileContents));

            sarifLog = JsonConvert.DeserializeObject<SarifLog>(sarifLogFileContents);

            FileWorkItems(sarifLog);
        }

        public virtual void FileWorkItems(SarifLog sarifLog)
        {
            this.FilingSucceeded = false;
            this.FiledWorkItems = new List<WorkItemModel>();

            sarifLog = sarifLog ?? throw new ArgumentNullException(nameof(sarifLog));

            this.FilingClient.Connect(this.FilingContext.PersonalAccessToken).Wait();

            OptionallyEmittedData optionallyEmittedData = this.FilingContext.DataToRemove;
            if (optionallyEmittedData != OptionallyEmittedData.None)
            {
                var dataRemovingVisitor = new RemoveOptionalDataVisitor(optionallyEmittedData);
                dataRemovingVisitor.Visit(sarifLog);
            }

            optionallyEmittedData = this.FilingContext.DataToInsert;
            if (optionallyEmittedData != OptionallyEmittedData.None)
            {
                var dataInsertingVisitor = new InsertOptionalDataVisitor(optionallyEmittedData);
                dataInsertingVisitor.Visit(sarifLog);
            }

            SplittingStrategy splittingStrategy = this.FilingContext.SplittingStrategy;
            if (splittingStrategy == SplittingStrategy.None)
            {
                FileWorkItemsHelper(sarifLog, this.FilingContext, this.FilingClient);
                return;
            }

            IList<SarifLog> logsToProcess = new List<SarifLog>(new SarifLog[] { sarifLog });

            PartitionFunction<string> partitionFunction = null;

            switch (splittingStrategy)
            {
                case SplittingStrategy.PerRun:
                {
                    partitionFunction = (result) => result.ShouldBeFiled() ? "Include" : null;
                    break;
                }
                case SplittingStrategy.PerResult:
                {
                    partitionFunction = (result) => result.ShouldBeFiled() ? Guid.NewGuid().ToString() : null;
                    break;
                }
                default:
                {
                    throw new ArgumentOutOfRangeException($"SplittingStrategy: {splittingStrategy}");
                }
            }

            var partitioningVisitor = new PartitioningVisitor<string>(partitionFunction, deepClone: false);
            partitioningVisitor.VisitSarifLog(sarifLog);

            logsToProcess = new List<SarifLog>(partitioningVisitor.GetPartitionLogs().Values);

            for (int splitFileIndex = 0; splitFileIndex < logsToProcess.Count; splitFileIndex++)
            {
                SarifLog splitLog = logsToProcess[splitFileIndex];
                FileWorkItemsHelper(splitLog, this.FilingContext, this.FilingClient);
            }
        }

        internal const string PROGRAMMABLE_URIS_PROPERTY_NAME = "programmableWorkItemUris";

        private void FileWorkItemsHelper(SarifLog sarifLog, SarifWorkItemContext filingContext, FilingClient filingClient)
        {
            // The helper below will initialize the sarif work item model with a copy
            // of the root pipeline filing context. This context will then be initialized
            // based on the current sarif log file that we're processing.
            // First intializes the contexts provider to the value in the current filing client.
            filingContext.CurrentProvider = filingClient.CurrentProvider;
            var workItemModel = new SarifWorkItemModel(sarifLog, filingContext);

            try
            {
                // Populate the work item with the target organization/repository information.
                // In ADO, certain fields (such as the area path) will defaut to the 
                // project name and so this information is used in at least that context.
                workItemModel.OwnerOrAccount = filingClient.AccountOrOrganization;
                workItemModel.RepositoryOrProject = filingClient.ProjectOrRepository;

                foreach (SarifWorkItemModelTransformer transformer in workItemModel.Context.Transformers)
                {
                    transformer.Transform(workItemModel);
                }

                Task<IEnumerable<WorkItemModel>> task = filingClient.FileWorkItems(new[] { workItemModel });
                task.Wait();

                this.FiledWorkItems.AddRange(task.Result);

                foreach (Run run in sarifLog.Runs)
                {
                    if (run.Results == null) { continue; }

                    foreach (Result result in run.Results)
                    {
                        result.WorkItemUris ??= new List<Uri>();
                        result.WorkItemUris.Add(workItemModel.HtmlUri);

                        result.TryGetProperty(PROGRAMMABLE_URIS_PROPERTY_NAME, out List<Uri> programmableUris);

                        programmableUris ??= new List<Uri>();
                        programmableUris.Add(workItemModel.Uri);

                        result.SetProperty(PROGRAMMABLE_URIS_PROPERTY_NAME, programmableUris);
                    }
                }

                this.FilingSucceeded = true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
        }

        public void Dispose()
        {
            this.FilingClient?.Dispose();
            this.FilingClient = null;
        }
    }
}