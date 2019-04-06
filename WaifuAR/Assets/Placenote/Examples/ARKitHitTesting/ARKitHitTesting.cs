using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using System.IO;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using UnityEngine.XR.iOS; // Import ARKit Library
using System;

namespace ARKitHitTesting
{
    public class ARKitHitTesting : MonoBehaviour, PlacenoteListener
    {
        // Unity ARKit Session handler
        private UnityARSessionNativeInterface mSession;

        // UI game object references
        public GameObject initPanel;
        public GameObject mappingPanel;
        public GameObject localizedPanel;
        public GameObject mPlaneGenerator;

        public Text notifications;

        // to hold the last saved MapID
        private string savedMapID;
        private LibPlacenote.MapMetadata downloadedMetaData;

        private bool localized = false;
        public bool Localized { get => localized; set => localized = value; }

        private bool isNewMap = false;
        public bool IsNewMap { get => isNewMap; set => isNewMap = value; }

        // Required for using ARKit
        void Start()
        {
            // Start ARKit using the Unity ARKit Plugin
            downloadedMetaData = new LibPlacenote.MapMetadata();
            Input.location.Start();
            mSession = UnityARSessionNativeInterface.GetARSessionNativeInterface();
            StartARKit(); // ******

            FeaturesVisualizer.EnablePointcloud(); // Optional - to see the point features
            LibPlacenote.Instance.RegisterListener(this); // Register listener for onStatusChange and OnPose
            notifications.text = "Click New Map to start";
        }

        // Hide/show specific buttons
        // Add shape when button is clicked.
        public void OnNewMapClick()
        {
            if (!LibPlacenote.Instance.Initialized())
            {
                Debug.Log("SDK not yet initialized");
                return;
            }

            notifications.text = "Mapping: Tap screen to add markers";

            initPanel.SetActive(false);
            mappingPanel.SetActive(true);
            localizedPanel.SetActive(false);

            // ****** start plane detection
            ConfigureSession(true, true);
            mPlaneGenerator.GetComponent<PlacenoteARGeneratePlane>().StartPlaneDetection();

            FeaturesVisualizer.EnablePointcloud();
            LibPlacenote.Instance.StartSession();

            IsNewMap = true;
        }

        // Initialize ARKit. This will be standard in all AR apps
        private void StartARKit()
        {
            //Application.targetFrameRate = 60;
            ////ARKitWorldTrackingSessionConfiguration config = new ARKitWorldTrackingSessionConfiguration();
            ////config.planeDetection = UnityARPlaneDetection.Horizontal;
            ////config.alignment = UnityARAlignment.UnityARAlignmentGravity;
            ////config.getPointCloudData = true;
            ////config.enableLightEstimation = true;
            ////mSession.RunWithConfig(config);
            //ConfigureSession(false, false);

            notifications.text = "Initializing ARKit";
            Application.targetFrameRate = 60;
            ConfigureSession(false, false);
        }

        // Hide/show specific buttons; then start doing the map save magics
        // Save a map and upload it to Placenote cloud
        public void OnSaveMapClick()
        {
            mappingPanel.SetActive(false);
            initPanel.SetActive(true);
            localizedPanel.SetActive(false);

            FeaturesVisualizer.clearPointcloud();

            if (!LibPlacenote.Instance.Initialized())
            {
                notifications.text = "SDK not yet initialized";
                return;
            }

            // ******
            LibPlacenote.Instance.SaveMap(
            (mapId) =>
            {
                savedMapID = mapId;

                LibPlacenote.Instance.StopSession();
                FeaturesVisualizer.DisablePointcloud(); // ****** used to turn off PointCloud
                FeaturesVisualizer.clearPointcloud();

                WriteMapIDToFile(mapId);

                LibPlacenote.MapMetadataSettable metadata = CreateMetaDataObject();

                LibPlacenote.Instance.SetMetadata(mapId, metadata, (success) =>
                {
                    if (success)
                    {
                        Debug.Log("Meta data successfully saved");
                    }
                    else
                    {
                        Debug.Log("Meta data failed to save");
                    }
                });

                // XXXXXX
                // GetComponent<MarkerManager>().ClearModels();

                // Clear planes;
                mPlaneGenerator.GetComponent<PlacenoteARGeneratePlane>().ClearPlanes();

            },
            (completed, faulted, percentage) =>
            {
                if (completed)
                {
                    notifications.text = "Upload Complete:" + savedMapID;

                }
                else if (faulted)
                {
                    notifications.text = "Upload of Map: " + savedMapID + " failed";
                }
                else
                {
                    notifications.text = "Upload Progress: " + percentage.ToString("F2") + "/1.0)";
                }
            }
            );

            IsNewMap = false;
        }

        // Define which data should be saved for the current map
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

            // ****** JSON object for this gameObject (e.g.: MarkerManager in this case)
            JObject userdata = new JObject();

            // XXXXXX turn markers into JSON objects
            //JObject modelList = GetComponent<MarkerManager>().Models2JSON(); // The gameObject has to implement this method to be converted
            //userdata["modelList"] = modelList;

            // ****** turn planes into JSON objects
            if (mPlaneGenerator != null)
            {
                userdata["planes"] = mPlaneGenerator.GetComponent<PlacenoteARGeneratePlane>().GetCurrentPlaneList(); // ****** method to turn planes into JSON objects
            }
            else
            {
                Debug.Log("No plane generator object, not saving planes");
            }

            metadata.userdata = userdata;
            return metadata;
        }

        // Hide/show some buttons; then start doing the map loading magic
        // Load map and relocalize. Check OnStatusChange function for behaviour upon relocalization
        public void OnLoadMapClicked()
        {
            // ****** delete the planes.
            ConfigureSession(false, false);

            initPanel.SetActive(false);
            mappingPanel.SetActive(false);
            localizedPanel.SetActive(true);

            if (!LibPlacenote.Instance.Initialized())
            {
                notifications.text = "SDK not yet initialized";
                return;
            }

            // Reading the last saved MapID from file
            savedMapID = ReadMapIDFromFile(); // ******

            LibPlacenote.Instance.LoadMap(savedMapID,
            (completed, faulted, percentage) =>
            {
                if (completed)
                {
                    // Get the meta data as soon as the map is downloaded
                    LibPlacenote.Instance.GetMetadata(savedMapID, (LibPlacenote.MapMetadata obj) =>
                     {
                         if (obj != null)
                         {
                             downloadedMetaData = obj;
                         }
                         else
                         {
                             notifications.text = "Failed to download meta data";
                             return;
                         }
                     });

                    // Now try to localize the map
                    LibPlacenote.Instance.StartSession();
                    notifications.text = "Trying to Localize Map: " + savedMapID;
                }
                else if (faulted)
                {
                    notifications.text = "Failed to load ID: " + savedMapID;
                }
                else
                {
                    notifications.text = "Download Progress: " + percentage.ToString("F2") + "/1.0)";
                }
            }

            );
        }

        public void OnExitClicked()
        {
            LibPlacenote.Instance.StopSession();
            FeaturesVisualizer.DisablePointcloud();
            FeaturesVisualizer.clearPointcloud();

            // XXXXXX Clear markers
            //GetComponent<MarkerManager>().ClearModels();
            // Clear planes
            mPlaneGenerator.GetComponent<PlacenoteARGeneratePlane>().ClearPlanes();

            initPanel.SetActive(true);
            mappingPanel.SetActive(false);
            localizedPanel.SetActive(false);

            ConfigureSession(false, true); // ****** stop detection and delete existing planes

            Localized = false;
            notifications.text = "Exited: Click New Map or Load Map";
        }

        // ?????? Runs when a new pose is received from Placenote.
        public void OnPose(Matrix4x4 outputPose, Matrix4x4 arkitPose) { }

        // Runs when LibPlacenote sends a status change message like Localized!
        public void OnStatusChange(LibPlacenote.MappingStatus prevStatus, LibPlacenote.MappingStatus currStatus)
        {
            if (currStatus == LibPlacenote.MappingStatus.RUNNING && prevStatus == LibPlacenote.MappingStatus.LOST)
            {
                notifications.text = "Localized!";
                if (!Localized)
                {
                    Localized = true;

                    JToken metaData = downloadedMetaData.userdata;
                    //GetComponent<MarkerManager>().LoadModelsFromJSON(metaData); // XXXXXX Called after localization

                    // ****** initialize planes from stored metadata on first localization
                    if (mPlaneGenerator != null)
                    {
                        //JToken planeData = downloadedMetaData.userdata;
                        mPlaneGenerator.GetComponent<PlacenoteARGeneratePlane>().LoadPlaneList(metaData);
                    }
                    else
                    {
                        Debug.Log("No plane generator object, not saving planes");
                    }

                    // XXXXXX Set up Interactions
                    //SetUpInteractions();

                    // Placenote will automatically correct the camera position on localization.
                }
            }
            else if (currStatus == LibPlacenote.MappingStatus.RUNNING && prevStatus == LibPlacenote.MappingStatus.WAITING)
            {
                notifications.text = "Mapping";
            }
            else if (currStatus == LibPlacenote.MappingStatus.LOST)
            {
                notifications.text = "Searching for position lock";
            }
            else if (currStatus == LibPlacenote.MappingStatus.WAITING)
            {
            }
        }

        private void WriteMapIDToFile(string mapID)
        {
            string path = Application.persistentDataPath + "/mapID.txt";
            Debug.Log(path);
            StreamWriter writer = new StreamWriter(path, false);
            writer.WriteLine(mapID);
            writer.Close();

        }

        // ******
        private string ReadMapIDFromFile()
        {
            string path = Application.persistentDataPath + "/mapID.txt";
            StreamReader reader = new StreamReader(path);
            string returnValue = reader.ReadLine();
            Debug.Log(returnValue);
            reader.Close();

            return returnValue;
        }

        // XXXXXX
        //private void SetUpInteractions()
        //{
        //    GameObject.Find("Interactions").GetComponent<ModelMovementController>().CurModel = GetComponent<MarkerManager>().CurModel;
        //}

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
                mPlaneGenerator.GetComponent<PlacenoteARGeneratePlane>().ClearPlanes();
            }

            config.alignment = UnityARAlignment.UnityARAlignmentGravity;
            config.getPointCloudData = true;
            config.enableLightEstimation = true;

            UnityARSessionRunOption options = new UnityARSessionRunOption();
            //options = UnityARSessionRunOption.ARSessionRunOptionRemoveExistingAnchors | UnityARSessionRunOption.ARSessionRunOptionResetTracking;
            options = UnityARSessionRunOption.ARSessionRunOptionRemoveExistingAnchors;
            mSession.RunWithConfigAndOptions(config, options);

        }
    }
}
