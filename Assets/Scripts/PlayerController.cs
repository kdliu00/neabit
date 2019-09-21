﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace com.tuth.neabit {
    public class PlayerController : MonoBehaviour {

        #region Private Serialize Fields

        [SerializeField]
        GameObject cameraAnchor;

        [SerializeField]
        GameObject rigPrefab;

        [SerializeField]
        Rigidbody rb;

        [SerializeField]
        float maxThrust;

        [SerializeField]
        float thrustRate;

        [SerializeField]
        float maxVThrust;

        [SerializeField]
        float maxYaw;

        [SerializeField]
        float maxRoll;

        [SerializeField]
        float maxPitch;

        [SerializeField]
        float maxAssistedTheta;

        #endregion

        #region Private Fields

        PlayerManager playerManager;

        float thrust;
        float vThrustRate;
        float yawRate;
        float rollRate;
        float pitchRate;
        Vector3 playerRotation; // for custom rotation processing
        GameObject headTrack;
        GameObject leftTrack;
        GameObject rightTrack;
        RigWrapper playerRig;

        #endregion

        #region Public Fields

        public bool KEYBOARD_CONTROL = false;
        public bool ASSISTED_CONTROL = true;

        #endregion

        #region MonoBehaviour CallBacks

        // Start is called before the first frame update
        void Start() {
            playerManager = GetComponent<PlayerManager>();
            rb = GetComponent<Rigidbody>();
            playerRotation = transform.rotation.eulerAngles;

            // setting up camera rig
            GameObject playerCamera = Instantiate(rigPrefab, transform.position, Quaternion.identity);
            playerRig = playerCamera.GetComponent<RigWrapper>();
            headTrack = playerRig.headTrack;
            leftTrack = playerRig.leftTrack;
            rightTrack = playerRig.rightTrack;
            GetComponent<PlayerManager>().playerCamera = playerCamera;
            playerRig.anchor = cameraAnchor.transform;
        }

        // Update is called once per frame
        void Update() {
            
            // control scheme switch
            if (Input.GetKeyDown(KeyCode.Q)) {
                KEYBOARD_CONTROL = !KEYBOARD_CONTROL;
            }
            if (Input.GetKeyDown(KeyCode.E) || OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick)) {
                ASSISTED_CONTROL = !ASSISTED_CONTROL;
            }

            // networked firing
            if (Input.GetKeyDown(KeyCode.M) || OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger)) {
                playerManager.fire();
            }

            // main thrusting is independent of control scheme
            bool isThrusting = KEYBOARD_CONTROL ? Input.GetKey(KeyCode.Space) : handTriggerAverage() > 0.3f;
            thrust += thrustProcessing(isThrusting, thrustRate);
            thrust = Mathf.Clamp(thrust, 0, maxThrust);
            Vector3 thrustVector = Vector3.up * thrust;

            if (ASSISTED_CONTROL) {
                
                AssistedInputs movementInputs = GetAssistedMovementInputs();

                // input output
                transform.Translate(thrustVector * Time.deltaTime);

                // custom rotation processing, keeps track of rotation separately from offsets
                float actualTurnRate = (movementInputs.isBanking / maxAssistedTheta) * maxPitch;
                if (float.IsNaN(actualTurnRate)) {
                    actualTurnRate = 0;
                }
                playerRotation += new Vector3(0f, 0f, actualTurnRate) * Time.deltaTime;
                transform.rotation = Quaternion.Euler(playerRotation + new Vector3(-90, 0f, 0f));
                Vector3 offset = new Vector3(-movementInputs.isPitching, -movementInputs.isBanking, 0f);
                transform.Rotate(offset);
                playerRig.anchorYaw = Quaternion.Euler(0f, playerRotation.z + 180f, 0f);

            } else { // manual control scheme

                ManualInputs movementInputs = GetManualMovementInputs();

                // vertical thrusters
                vThrustRate = inputSmoothing(movementInputs.isVThrusting) * maxVThrust;
                Vector3 vThrustVector = Vector3.forward * vThrustRate;

                // thrust sum
                Vector3 combinedThrustVector = thrustVector + vThrustVector;

                // angular floats
                rollRate = inputSmoothing(movementInputs.isRolling) * maxRoll;
                yawRate = inputSmoothing(movementInputs.isYawing) * maxYaw;
                pitchRate = -inputSmoothing(movementInputs.isPitching) * maxPitch; // inverted pitch

                // input output
                transform.Translate(combinedThrustVector * Time.deltaTime);
                transform.Rotate(new Vector3(pitchRate, -rollRate, yawRate) * Time.deltaTime); // rollRate needs to be inverted here, probably because prefab rotations

            }
        }

        private void OnCollisionEnter(Collision other)
        {
            rb.angularVelocity = Vector3.zero; // stops violent rotations from collisions
        }
        
        #endregion

        #region Private Methods

        struct ManualInputs {
            public float isVThrusting { get; }
            public float isRolling { get; }
            public float isPitching { get; }
            public float isYawing { get; }
            public ManualInputs(float iV, float iR, float iP, float iY) {
                isVThrusting = iV;
                isRolling = iR;
                isPitching = iP;
                isYawing = iY;
            }
        }

        ManualInputs GetManualMovementInputs() {
            float iV = KEYBOARD_CONTROL ? Input.GetAxis("VerticalAlt") : OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick).y;
            float iR = KEYBOARD_CONTROL ? Input.GetAxis("Horizontal") : OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick).x;
            float iP = KEYBOARD_CONTROL ? Input.GetAxis("Vertical") : OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick).y;
            float iY = KEYBOARD_CONTROL ? Input.GetAxis("HorizontalAlt") : OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick).x;
            return new ManualInputs(iV, iR, iP, iY);
        }

        struct AssistedInputs {
            public float isBanking { get; }
            public float isPitching { get; }
            public AssistedInputs(float iB, float iP) {
                isBanking = iB;
                isPitching = iP;
            }
        }

        AssistedInputs GetAssistedMovementInputs() {
            Vector3 leftTouchP = leftTrack.transform.localPosition;
            Vector3 rightTouchP = rightTrack.transform.localPosition;
            Vector3 headP = headTrack.transform.localPosition;

            Vector3 averageHandP = 0.5f * (leftTouchP + rightTouchP);
            Vector3 shoulderOffset = new Vector3(0f, 0.4f, 0f);

            float iB = KEYBOARD_CONTROL ? Input.GetAxis("Horizontal") * maxAssistedTheta : -calculateTheta(leftTouchP, rightTouchP, maxAssistedTheta);
            float iP = KEYBOARD_CONTROL ? Input.GetAxis("Vertical") * maxAssistedTheta : calculateTheta(averageHandP, headP - shoulderOffset, maxAssistedTheta);

            return new AssistedInputs(iB, iP);
        }

        // input processing, apply polynomial for control smoothing
        float inputSmoothing(float input) {
            return Mathf.Sign(input) * Mathf.Pow(Mathf.Abs(input), 2); // returns squared and signed input
        }

        float thrustProcessing(bool isThrusting, float rate) {
            if (isThrusting) {
                return rate;
            } else {
                return -rate;
            }
        }

        float handTriggerAverage() {
            return 0.5f * ( OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger) + OVRInput.Get(OVRInput.Axis1D.SecondaryHandTrigger) );
        }

        // for assisted control scheme
        float calculateTheta(Vector3 a, Vector3 b, float maxTheta) {
            float euclidean = Vector3.Distance(a, b);
            float vDist = a.y - b.y;

            float angle = Mathf.Asin(vDist / euclidean) * Mathf.Rad2Deg;

            return -Mathf.Clamp(angle, -maxTheta, maxTheta);
        }

        #endregion
    }
}
