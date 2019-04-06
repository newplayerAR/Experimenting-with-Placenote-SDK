using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.iOS;

namespace ARKitPlaneSaver
{
    public class ModelMovementController : MonoBehaviour
    {
        public float distBeforeCam = 0.5f;
        private GameObject curModel;

        public GameObject CurModel { get => curModel; set => curModel = value; }

        public void OnComeHereClicked()
        {
            if (CurModel == null)
            {
                Debug.Log("Error: curModel is null.");
                return;
            }

            Vector3 camPos = Camera.main.transform.position + Camera.main.transform.forward * distBeforeCam;
            CurModel.transform.position = camPos;
        }

        public void OnComeHereClicked_NmA()
        {
            // TODO:: With NavmeshAgent
        }
    }
}