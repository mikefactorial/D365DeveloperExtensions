﻿using D365DeveloperExtensions.Core;
using D365DeveloperExtensions.Core.Enums;
using D365DeveloperExtensions.Core.Logging;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Tooling.Connector;
using NLog;
using PluginDeployer.Resources;
using PluginDeployer.Spkl;
using PluginDeployer.ViewModels;
using System;

namespace PluginDeployer.Crm
{
    public class Assembly
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static Entity RetrieveAssemblyFromCrm(CrmServiceClient client, string assemblyName)
        {
            try
            {
                QueryExpression query = new QueryExpression
                {
                    EntityName = "pluginassembly",
                    ColumnSet = new ColumnSet("pluginassemblyid", "name", "version"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression
                            {
                                AttributeName = "name",
                                Operator = ConditionOperator.Equal,
                                Values = { assemblyName }
                            }
                        }
                    }
                };

                EntityCollection assemblies = client.RetrieveMultiple(query);

                if (assemblies.Entities.Count <= 0)
                    return null;

                OutputLogger.WriteToOutputWindow($"{Resource.Message_RetrievedAssembly}: {assemblies.Entities[0].Id}", MessageType.Info);
                return assemblies.Entities[0];

            }
            catch (Exception ex)
            {
                ExceptionHandler.LogException(Logger, Resource.ErrorMessage_ErrorRetrievingAssembly, ex);

                return null;
            }
        }

        public static Guid UpdateCrmAssembly(CrmServiceClient client, CrmAssembly crmAssembly)
        {
            try
            {
                Entity assembly = new Entity("pluginassembly")
                {
                    ["content"] = Convert.ToBase64String(FileSystem.GetFileBytes(crmAssembly.AssemblyPath)),
                    ["name"] = crmAssembly.Name,
                    ["culture"] = crmAssembly.Culture,
                    ["version"] = crmAssembly.Version,
                    ["publickeytoken"] = crmAssembly.PublicKeyToken,
                    ["sourcetype"] = new OptionSetValue(0), // database
                    ["isolationmode"] = crmAssembly.IsolationMode == IsolationModeEnum.Sandbox
                    ? new OptionSetValue(2) // 2 = sandbox
                    : new OptionSetValue(1) // 1= none
                };

                if (crmAssembly.AssemblyId == Guid.Empty)
                {
                    Guid newId = client.Create(assembly);
                    OutputLogger.WriteToOutputWindow($"{Resource.Message_CreatedAssembly}: {newId}", MessageType.Info);
                    return newId;
                }

                assembly.Id = crmAssembly.AssemblyId;
                client.Update(assembly);

                OutputLogger.WriteToOutputWindow($"{Resource.Message_UpdatedAssembly}: {crmAssembly.AssemblyId}", MessageType.Info);

                return crmAssembly.AssemblyId;
            }
            catch (Exception ex)
            {
                ExceptionHandler.LogException(Logger, Resource.ErrorMessage_ErrorCreatingOrUpdatingAssembly, ex);

                return Guid.Empty;
            }
        }

        public static bool AddAssemblyToSolution(CrmServiceClient client, Guid assemblyId, string uniqueName)
        {
            try
            {
                AddSolutionComponentRequest scRequest = new AddSolutionComponentRequest
                {
                    ComponentType = 91,
                    SolutionUniqueName = uniqueName,
                    ComponentId = assemblyId
                };

                client.Execute(scRequest);

                OutputLogger.WriteToOutputWindow($"{Resource.Message_AssemblyAddedSolution}: {uniqueName} - {assemblyId}", MessageType.Info);

                return true;
            }
            catch (Exception ex)
            {
                ExceptionHandler.LogException(Logger, Resource.ErrorMessage_ErrorAddingAssemblySolution, ex);

                return false;
            }
        }

        public static bool IsAssemblyInSolution(CrmServiceClient client, string assemblyName, string uniqueName)
        {
            try
            {
                FetchExpression query = new FetchExpression($@"<fetch>
                                                          <entity name='solutioncomponent'>
                                                            <attribute name='solutioncomponentid'/>
                                                            <link-entity name='pluginassembly' from='pluginassemblyid' to='objectid'>
                                                              <attribute name='pluginassemblyid'/>
                                                              <filter type='and'>
                                                                <condition attribute='name' operator='eq' value='{assemblyName}'/>
                                                              </filter>
                                                            </link-entity>
                                                            <link-entity name='solution' from='solutionid' to='solutionid'>
                                                              <attribute name='solutionid'/>
                                                              <filter type='and'>
                                                                <condition attribute='uniquename' operator='eq' value='{uniqueName}'/>
                                                              </filter>
                                                            </link-entity>
                                                          </entity>
                                                        </fetch>");

                EntityCollection results = client.RetrieveMultiple(query);

                bool inSolution = results.Entities.Count > 0;

                OutputLogger.WriteToOutputWindow($"{Resource.Message_AssemblyInSolution}: {uniqueName} - {assemblyName} - {inSolution}", MessageType.Info);

                return inSolution;
            }
            catch (Exception ex)
            {
                ExceptionHandler.LogException(Logger, Resource.ErrorMessage_ErrorCheckingAssemblyInSolution, ex);

                return true;
            }
        }
    }
}