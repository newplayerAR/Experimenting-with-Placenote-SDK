﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;
using UnityEngine.XR.iOS;
using System.Runtime.InteropServices;
using System.IO;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace ARKitPlaneSaver
{
    public class ARKitPlaneSaver : MonoBehaviour, PlacenoteListener
    {
        [SerializeField] GameObject mInitButtonPanel;
        [SerializeField] GameObject mMappingButtonPanel;
        [SerializeField] GameObject localizedPanel;
        [SerializeField] Text mLabelText;
        [SerializeField] PlacenoteARGeneratePlane mPlaneGenerator;
        [SerializeField] ModelManager mModelManager;

        private UnityARSessionNativeInterface mSession;

        private LibPlacenote.MapInfo mCurrMapInfo;
        private string mCurrMapId;
        private LibPlacenote.MapMetadata downloadedMetaData;

        private bool mFrameUpdated = false;
        private UnityARImageFrameData mImage = null;
        private UnityARCamera mARCamera;
        private bool mARKitInit = false;
        private bool localizedFirstTime = false;
        private bool atNewMap = false;

        public bool LocalizedFirstTime { get => localizedFirstTime; set => localizedFirstTime = value; }
        public bool AtNewMap { get => atNewMap; set => atNewMap = value; }

        // Use this for initialization
        void Start()
        {
            downloadedMetaData = new LibPlacenote.MapMetadata();
            Input.location.Start();
            mSession = UnityARSessionNativeInterface.GetARSessionNativeInterface();
            StartARKit();
            FeaturesVisualizer.EnablePointcloud();
            LibPlacenote.Instance.RegisterListener(this);
        }

        public void OnExitClick()
        {
            mInitButtonPanel.SetActive(true);
            localizedPanel.SetActive(false);
            LibPlacenote.Instance.StopSession();
            FeaturesVisualizer.DisablePointcloud();
            FeaturesVisualizer.clearPointcloud();

            ConfigureSession(false, true); // ****** stop detection and delete existing planes
            mModelManager.ClearModels(); // xxxxxx Clear markers

            LocalizedFirstTime = false; // ****** similar to Localized
            mLabelText.text = "Exited: Click New Map or Load Map";
        }


        // Load map and relocalize. Check OnStatusChange function for behaviour upon relocalization
        public void OnLoadMapClicked()
        {
            // ****** delete the planes.
            ConfigureSession(false, false);

            mInitButtonPanel.SetActive(false);
            mMappingButtonPanel.SetActive(false);
            localizedPanel.SetActive(true);

            if (!LibPlacenote.Instance.Initialized())
            {
                mLabelText.text = "SDK not yet initialized";
                return;
            }

            // Reading the last saved MapID from file
            mCurrMapId = LoadMapIDFromFile();

            LibPlacenote.Instance.LoadMap(mCurrMapId,
            (completed, faulted, percentage) =>
            {
                if (completed)
                {
                    // Get the meta data as soon as the map is downloaded
                    LibPlacenote.Instance.GetMetadata(mCurrMapId, (LibPlacenote.MapMetadata obj) =>
                        {
                            if (obj != null)
                            {
                                downloadedMetaData = obj;

                                // Now try to localize the map
                                mLabelText.text = "Trying to Localize Map: " + mCurrMapId;
                                LibPlacenote.Instance.StartSession();
                            }
                            else
                            {
                                mLabelText.text = "Failed to download meta data";
                                return;
                            }
                        });

                }
                else if (faulted)
                {
                    mLabelText.text = "Failed to load ID: " + mCurrMapId;
                }
                else
                {
                    mLabelText.text = "Download Progress: " + percentage.ToString("F2") + "/1.0)";
                }
            }

            );
        }

        // ???
        public void OnDeleteMapClicked()
        {
            if (!LibPlacenote.Instance.Initialized())
            {
                Debug.Log("SDK not yet initialized");
                return;
            }

            mLabelText.text = "Deleting Map ID: " + mCurrMapId;
            LibPlacenote.Instance.DeleteMap(mCurrMapId, (deleted, errMsg) =>
            {
                if (deleted)
                {
                    mLabelText.text = "Deleted ID: " + mCurrMapId;
                }
                else
                {
                    mLabelText.text = "Failed to delete ID: " + mCurrMapId;
                }
            });
        }


        public void OnNewMapClick()
        {
            if (!LibPlacenote.Instance.Initialized())
            {
                Debug.Log("SDK not yet initialized");
                return;
            }
            mInitButtonPanel.SetActive(false);
            mMappingButtonPanel.SetActive(true);


            // ****** start plane detection
            ConfigureSession(true, true);
            mPlaneGenerator.StartPlaneDetection();

            FeaturesVisualizer.EnablePointcloud();
            LibPlacenote.Instance.StartSession();

            atNewMap = true;

        }

        /*
        private void StartARKit()
        {
            mLabelText.text = "Initializing ARKit";
            Application.targetFrameRate = 60;
            ARKitWorldTrackingSessionConfiguration config = new ARKitWorldTrackingSessionConfiguration();
            config.planeDetection = UnityARPlaneDetection.Horizontal;
            config.alignment = UnityARAlignment.UnityARAlignmentGravity;
            config.getPointCloudData = true;
            config.enableLightEstimation = true;
            mSession.RunWithConfig(config);
        }
        */

        private void StartARKit()
        {
            mLabelText.text = "Initializing ARKit";
            Application.targetFrameRate = 60;
            ConfigureSession(false, false); // ****** must know what this function does very clearly
        }

        // ****** important in plane generation
        private void ConfigureSession(bool togglePlaneDetection, bool clearOldPlanes)
        {

            ARKitWorldTrackingSessionConfiguration config = new ARKitWorldTrackingSessionConfiguration();

            // Turn ON/OFF plane detection
            if (togglePlaneDetection)
            {
                if (UnityARSessionNativeInterface.IsARKit_1_5_Supported())
                {
                    config.planeDetection = UnityARPlaneDetection.HorizontalAndVertical;
                }
                else
                {
                    config.planeDetection = UnityARPlaneDetection.Horizontal;
                }

            }
            else
            {
                config.planeDetection = UnityARPlaneDetection.None;
            }

            // Clear current planes
            if (clearOldPlanes)
            {
                mPlaneGenerator.ClearPlanes();
            }

            config.alignment = UnityARAlignment.UnityARAlignmentGravity;
            config.getPointCloudData = true;
            config.enableLightEstimation = true;

            UnityARSessionRunOption options = new UnityARSessionRunOption();
            //options = UnityARSessionRunOption.ARSessionRunOptionRemoveExistingAnchors | UnityARSessionRunOption.ARSessionRunOptionResetTracking;
            options = UnityARSessionRunOption.ARSessionRunOptionRemoveExistingAnchors;
            mSession.RunWithConfigAndOptions(config, options);

        }

        public void OnSaveMapClick()
        {
            if (!LibPlacenote.Instance.Initialized())
            {
                Debug.Log("SDK not yet initialized");

                return;
            }

            //bool useLocation = Input.location.status == LocationServiceStatus.Running;
            //LocationInfo locationInfo = Input.location.lastData;

            mLabelText.text = "Saving...";
            atNewMap = false;
            LibPlacenote.Instance.SaveMap(
                (mapId) =>
                {
                    LibPlacenote.Instance.StopSession();
                    FeaturesVisualizer.DisablePointcloud(); // ****** used to turn off PointCloud
                    FeaturesVisualizer.clearPointcloud();
                    
                    mLabelText.text = "Saved Map ID: " + mapId;
                    mInitButtonPanel.SetActive(true);
                    mMappingButtonPanel.SetActive(false);

                    LibPlacenote.MapMetadataSettable metadata = CreateMetaDataObject(); // ****** prepare metadata to be saved

                    LibPlacenote.Instance.SetMetadata(mapId, metadata, (success) =>
                    {
                        if (success)
                        {
                            mLabelText.text = "Meta data successfully saved";
                        }
                        else
                        {
                            mLabelText.text = "Meta data failed to save";
                        }
                    });

                    SaveMapIDToFile(mapId);
                    
                },
                (completed, faulted, percentage) =>
                {

                    if (completed)
                    {
                        mLabelText.text = "Upload Complete:";

                        // delete the planes.
                        ConfigureSession(false, true);

                        // xxxxxx delete the models.
                        mModelManager.ClearModels();
                    }
                    else if (faulted)
                    {
                        mLabelText.text = "Upload of Map failed";
                    }
                    else
                    {
                        mLabelText.text = "Upload Progress: " + percentage.ToString("F2") + "/1.0)";
                    }
                }
            );
        }

        public LibPlacenote.MapMetadataSettable CreateMetaDataObject()
        {
            LibPlacenote.MapMetadataSettable metadata = new LibPlacenote.MapMetadataSettable();

            metadata.name = "My test map";

            // get GPS location of device to save with map
            bool useLocation = Input.location.status == LocationServiceStatus.Running;
            LocationInfo locationInfo = Input.location.lastData;
            if (useLocation)
            {
                metadata.location = new LibPlacenote.MapLocation();
                metadata.location.latitude = locationInfo.latitude;
                metadata.location.longitude = locationInfo.longitude;
                metadata.location.altitude = locationInfo.altitude;
            }

            JObject userdata = new JObject();

            // xxxxxx turn models into json
            if (mModelManager != null)
            {
                userdata["modelList"] = mModelManager.Models2JSON();
            }
            else
            {
                Debug.Log("No model manager object, not saving models");
            }

            if (mPlaneGenerator != null)
            {
                userdata["planes"] = mPlaneGenerator.GetCurrentPlaneList(); // ****** method to turn planes into JSON objects
            }
            else
            {
                Debug.Log("No plane generator object, not saving planes");
            }

            metadata.userdata = userdata;
            return metadata;
        }


        public void SaveMapIDToFile(string mapid)
        {
            string filePath = Application.persistentDataPath + "/mapIDFile.txt";
            StreamWriter sr = File.CreateText(filePath);
            sr.WriteLine(mapid);
            sr.Close();
        }

        public string LoadMapIDFromFile()
        {
            string savedMapID;
            // read history file
            FileInfo historyFile = new FileInfo(Application.persistentDataPath + "/mapIDFile.txt");
            StreamReader sr = historyFile.OpenText();
            string text;
            do
            {
                text = sr.ReadLine();
                if (text != null)
                {
                    // Create drawing command structure from string.
                    savedMapID = text;
                    return savedMapID;

                }
            } while (text != null);
            return null;
        }

        public void OnPose(Matrix4x4 outputPose, Matrix4x4 arkitPose) { }


        public void OnStatusChange(LibPlacenote.MappingStatus prevStatus, LibPlacenote.MappingStatus currStatus)
        {
            Debug.Log("prevStatus: " + prevStatus.ToString() + " currStatus: " + currStatus.ToString());
            if (currStatus == LibPlacenote.MappingStatus.RUNNING && prevStatus == LibPlacenote.MappingStatus.LOST)
            {
                mLabelText.text = "Localized State!";

                if (!LocalizedFirstTime)
                {
                    LocalizedFirstTime = true; // ****** this is basically Localized

                    mLabelText.text = "Localized: loaded shapes";


                    JToken metaData = downloadedMetaData.userdata; // ****** initialize from stored metadata on first localization

                    if (mPlaneGenerator != null)
                    {
                        mPlaneGenerator.LoadPlaneList(metaData);
                    }
                    else
                    {
                        Debug.Log("No plane generator object, not loading planes");
                    }

                    if (mModelManager != null)
                    {
                        mModelManager.LoadModelsFromJSON(metaData); // xxxxxx Called after localization to generate stored models
                    }
                    else
                    {
                        Debug.Log("No model manager object, not loading models");
                    }

                    // xxxxxx Set up Interactions
                    SetUpInteractions();
                }
            }
            else if (currStatus == LibPlacenote.MappingStatus.RUNNING && prevStatus == LibPlacenote.MappingStatus.WAITING)
            {
                mLabelText.text = "Mapping";
            }
            else if (currStatus == LibPlacenote.MappingStatus.LOST)
            {
                mLabelText.text = "Searching for position lock";
            }
            else if (currStatus == LibPlacenote.MappingStatus.WAITING)
            {
            }
        }

        // xxxxxx Set up controllable target(s)
        private void SetUpInteractions()
        {
            GameObject.Find("Interactions").GetComponent<ModelMovementController>().CurModel = mModelManager.CurModel;
        }
    }
}
