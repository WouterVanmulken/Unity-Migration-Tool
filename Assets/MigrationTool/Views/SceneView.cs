﻿#if UNITY_EDITOR

using migrationtool.controllers;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using migrationtool.models;
using migrationtool.utility;
using migrationtool.windows;
using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace migrationtool.views
{
    public class SceneView
    {
        private Constants constants = Constants.Instance;

        private IDController idController = new IDController();
        private FieldMappingController fieldMappingController = new FieldMappingController();

        private MergingWizard mergingWizard;
        private Thread calculationThread;

        private static List<ClassModel> oldFileDatas;

        public void ImportClassDataAndScene()
        {
            // todo : parse scene file that have prefabs in prefabs and have the "skipped" in it, causing the yaml lib to brake

            if (calculationThread != null)
            {
                if (!EditorUtility.DisplayDialog("Already running import",
                    "Can't Start new import while import is running", "Resume", "Stop"))
                {
                    calculationThread.Abort();
                    calculationThread = null;
                }

                return;
            }

//            EditorUtility.DisplayDialog("Please select the scene", "Please select the scene to migrate.",
//                "Select the scene");
            string scenePath =
                EditorUtility.OpenFilePanel("Scene to import", Application.dataPath,
                    "unity"); //todo : check if this is in the current project
            if (scenePath.Length == 0)
            {
                Debug.LogWarning("No path was selected");
                return;
            }

            string IDPath = ProjectPathUtility.getProjectPathFromFile(scenePath) + constants.RelativeExportPath;

            if (!File.Exists(IDPath))
            {
                EditorUtility.DisplayDialog("Could not find old ID's",
                    "Could not find the ID's of the original project.  File does not exist : \r\n" + IDPath, "Ok");
                return;
            }

            List<ClassModel> oldIDs =
                JsonConvert.DeserializeObject<List<ClassModel>>(File.ReadAllText(IDPath));

            string rootPath = Application.dataPath;
            string newIDsPath = rootPath + constants.RelativeExportPath;

            List<ClassModel> newIDs = File.Exists(newIDsPath)
                ? JsonConvert.DeserializeObject<List<ClassModel>>(File.ReadAllText(newIDsPath))
                : idController.ExportClassData(rootPath);


            List<FoundScript> foundScripts = new List<FoundScript>();
            string foundScriptsPath = rootPath + constants.RelativeFoundScriptPath;
            if (File.Exists(foundScriptsPath))
            {
                foundScripts =
                    JsonConvert.DeserializeObject<List<FoundScript>>(File.ReadAllText(foundScriptsPath));
            }

            calculationThread =
                new Thread(() => this.ImportTransformIDs(rootPath, oldIDs, newIDs, scenePath, foundScripts));
            calculationThread.Start();
        }


        /// <summary>
        /// Make a copy of the scene file and change the GUIDs, fileIDs and if necessary the fields 
        /// </summary>
        /// <param name="rootPath"></param>
        /// <param name="oldIDs"></param>
        /// <param name="currentIDs"></param>
        /// <param name="scenePath"></param>
        /// <param name="foundScripts"></param>
        public void ImportTransformIDs(string rootPath, List<ClassModel> oldIDs, List<ClassModel> currentIDs,
            string scenePath,
            List<FoundScript> foundScripts)
        {
            try
            {
                if (constants.DEBUG)
                {
                    Debug.LogWarning("[DEBUG ACTIVE] Using old ids for the import");
                }

                if (oldIDs == null || currentIDs == null)
                {
                    throw new NullReferenceException("One of the ids is null");
                }

                string[] lastSceneExport =
                    idController.TransformIDs(scenePath, oldIDs, currentIDs,
                        ref foundScripts);

                MigrationWindow.Instance().Enqueue(() =>
                {
                    ImportAfterIDTransformationOnMainThread(rootPath, scenePath, foundScripts, lastSceneExport);
                });
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                throw;
            }
        }

        private void
            ImportAfterIDTransformationOnMainThread(string rootPath, string scenePath,
                List<FoundScript> foundScripts,
                string[] lastSceneExport)
        {
            foreach (FoundScript script in foundScripts)
            {
                if (script.HasBeenMapped == FoundScript.MappedState.NotChecked)
                {
                    throw new NotImplementedException("Script has not been checked for mapping");
                }
            }


            FoundScript[] unmappedScripts = foundScripts
                .Where(field => field.HasBeenMapped == FoundScript.MappedState.NotMapped).ToArray();
            if (unmappedScripts.Length > 0)
            {
                // Remove duplicate scripts
                List<FoundScript> scripts =
                    unmappedScripts
                        .GroupBy(field => field.newClassModel.FullName)
                        .Select(group => group.First()).ToList();

                EditorUtility.DisplayDialog("Merging fields necessary",
                    "Could not merge all the fields to the class in the new project. You'll have to manually match old fields with the new fields",
                    "Open merge window");

                mergingWizard = MergingWizard.CreateWizard(scripts);

                mergingWizard.onComplete = (userAuthorizedList) =>
                {
                    MergingWizardCompleted(foundScripts, rootPath, scenePath, lastSceneExport, userAuthorizedList);
                };
            }
            else
            {
//                SaveFoundScripts(rootPath, foundScripts);
//                SaveFile(rootPath + "/" + Path.GetFileName(scenePath), lastSceneExport);
//                calculationThread = null;
                MergingWizardCompleted(foundScripts, rootPath, scenePath, lastSceneExport);
            }
        }

        /// <summary>
        /// Change the fields after merging with the merging window
        /// </summary>
        /// <param name="originalFoundScripts"></param>
        /// <param name="rootPath"></param>
        /// <param name="scenePath"></param>
        /// <param name="linesToChange"></param>
        /// <param name="mergedFoundScripts"></param>
        private void MergingWizardCompleted(List<FoundScript> originalFoundScripts, string rootPath,
            string scenePath,
            string[] linesToChange,
            List<FoundScript> mergedFoundScripts = null)
        {
            if (mergedFoundScripts != null)
            {
                originalFoundScripts = originalFoundScripts.Merge(mergedFoundScripts);
            }

            fieldMappingController.MigrateFields(scenePath, ref linesToChange, originalFoundScripts,
                ProjectPathUtility.getProjectPathFromFile(scenePath), rootPath);


            Debug.Log("Exported scene, Please press   Ctrl + R   to view it in the project tab. File:  " + rootPath +
                      "/" + Path.GetFileName(scenePath) + "");

            SaveFoundScripts(rootPath, originalFoundScripts);
            SaveFile(rootPath + "/" + Path.GetFileName(scenePath), linesToChange);
            calculationThread = null;


            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Write foundScripts to a file
        /// </summary>
        /// <param name="rootPath"></param>
        /// <param name="foundScripts"></param>
        private void SaveFoundScripts(string rootPath, List<FoundScript> foundScripts)
        {
            string foundScriptsPath = rootPath + constants.RelativeFoundScriptPath;
            File.WriteAllText(foundScriptsPath, JsonConvert.SerializeObject(foundScripts, Formatting.Indented));
        }

        /// <summary>
        /// Saves the <param name="linesToWrite"/> to a new file at the <param name="scenePath"/>
        /// </summary>
        /// <param name="scenePath"></param>
        /// <param name="linesToWrite"></param>
        private void SaveFile(string scenePath, string[] linesToWrite)
        {
            var now = DateTime.Now;
            string newScenePath = scenePath + "_imported_" + now.Hour + "_" + now.Minute + "_" +
                                  now.Second + ".unity";

            if (!Directory.Exists(Path.GetDirectoryName(newScenePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(newScenePath));
            }

            File.WriteAllText(newScenePath, string.Join("\n", linesToWrite));
            EditorUtility.DisplayDialog("Imported data", "The scene was exported to " + newScenePath, "Ok");
        }
    }
}
#endif