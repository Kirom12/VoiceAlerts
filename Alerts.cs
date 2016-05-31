using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using SpeechLib;

namespace VoiceAlerts
{
    [KSPAddon(KSPAddon.Startup.Flight, false)] //Launch every time at flight loading
    public class Alerts : MonoBehaviour
    {
        //Intervals
        private const float _defaultInterval = 1.0f;
        private float _heatIntervalAlert = 2.0f;
        private float _debugInterval = 10.0f;
        private float _interval = _defaultInterval;


        private const float _alarmHeat = 0.85f;
        private float _lastUpdate;
        private SpVoice _vAlert = new SpVoice();

        //List for alert at different altitude, bool for no repeat
        private int[] altitudeAlertsMeters = { 2000, 5000, 10000, 15000, 20000, 30000, 40000, 50000, 60000};
        private bool[] altitudeAlertsAlreadySend = { false, false, false, false, false, false, false, false, false};

        //Different alert for orbits & sub orbital
        private bool apoapsisReachAlert = false;
        private bool orbitingReachAlert = false;
        private bool orbitingLeaveAlert = false;

        private bool preLaunchAlert = false;

        /// <summary>
        /// Speech a alert
        /// </summary>
        /// <param name="args"></param>
        public void Speech(string args)
        {
            _vAlert.Rate = 0;
            _vAlert.Volume = 100;
            _vAlert.SynchronousSpeakTimeout = 30;
            _vAlert.Speak(args, SpeechVoiceSpeakFlags.SVSFlagsAsync);
        }

        /// <summary>
        /// Stop the current speech
        /// </summary>
        private void StopVoice()
        {
            _vAlert.Skip("Sentence", Int32.MaxValue);
        }

        //When Flight Scene is load
        private void Awake()
        {
            LogVa("VoiceAlerts loaded !");
            Speech("Voice Alerts Loaded !");


            //GameEvents
            GameEvents.onLaunch.Add(OnLaunch);
            GameEvents.onOverheat.Add(OnOverheat);
            GameEvents.onStageSeparation.Add(OnStageSeparation);
            //GameEvents.onVesselOrbitClosed.Add(OnVesselOrbitClosed);
            //GameEvents.onVesselOrbitEscaped.Add(OnVesselOrbitEscaped);
            GameEvents.onVesselSOIChanged.Add(OnVesselSOIChanged);
        }

        private void Start()
        {
            //Check if alerts should be send or not OR already send
            if (FlightGlobals.ActiveVessel.situation.ToString() == "LANDED" || FlightGlobals.ActiveVessel.situation.ToString() == "PRELAUNCH") //Ship landed or prelaunch (LANDED/PRELAUNCH)
            {
                //Things here
            }
            else //Ship already take off/on orbit/sub orbital (FLYING/SUB_ORBITAL/ORBITING/ESCAPING/DOCKED)
            {
                apoapsisReachAlert = true;
                if (FlightGlobals.ActiveVessel.situation.ToString() != "SUB_ORBITAL") //Ship already on orbit (FLYING/ORBITING/ESCAPING/DOCKED)
                {
                    orbitingReachAlert = true;
                }
            }

            if (FlightGlobals.ActiveVessel.situation.ToString() == "LANDED" || FlightGlobals.ActiveVessel.situation.ToString() == "PRELAUNCH" || FlightGlobals.ActiveVessel.situation.ToString() == "SUB_ORBITAL" || FlightGlobals.ActiveVessel.situation.ToString() == "FLYING")
            {
                orbitingLeaveAlert = true;
            }
        }

        private void OnDestroy()
        {
            GameEvents.onLaunch.Remove(OnLaunch);
            GameEvents.onOverheat.Remove(OnOverheat);
            GameEvents.onStageSeparation.Remove(OnStageSeparation);
            //GameEvents.onVesselOrbitClosed.Remove(OnVesselOrbitClosed);
            //GameEvents.onVesselOrbitEscaped.Remove(OnVesselOrbitEscaped);
            GameEvents.onVesselSOIChanged.Remove(OnVesselSOIChanged);
        }

        private void OnLaunch(EventReport data)
        {
            StopVoice();
            if (FlightGlobals.fetch.VesselTarget != null)
            {
                Speech("LIFTOFF !" + FlightGlobals.ActiveVessel.GetName() + " starts his mission to " + FlightGlobals.fetch.VesselTarget.GetName() + "!");
            }
            else
            {
                Speech("LIFTOFF !" + FlightGlobals.ActiveVessel.GetName() + " starts his mission!");
            }
        }

        private void OnOverheat(EventReport data)
        {
            StopVoice();
            Speech(data.origin.name + " destroyed");
        }

        private void OnVesselSOIChanged(GameEvents.HostedFromToAction<Vessel, CelestialBody> data)
        {
            if (data.host == FlightGlobals.ActiveVessel)
            {
                Speech(data.host.GetName() + " is now orbiting " + data.to.GetName());
            }
        }

        private void OnStageSeparation(EventReport data)
        {
            StopVoice();
            UnityEngine.Debug.Log("====[VoiceAlerts]==== " + data.stage);
            Speech("Stage " + data.stage + " decoupled");
        }

        private void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight || FlightGlobals.ActiveVessel.isEVA) return;
            if ((Time.time - _lastUpdate) > _interval)
            {
                //Reset the interval
                _interval = _defaultInterval;
                _lastUpdate = Time.time;

                //Prelaunch message, prepare liftoff
                if (FlightGlobals.ActiveVessel.situation.ToString() == "PRELAUNCH" && preLaunchAlert == false)
                {
                    preLaunchAlert = true;
                    Speech(FlightGlobals.ActiveVessel.GetName() + " is on the launchpad and ready to launch");
                }

                foreach (Part part in FlightGlobals.ActiveVessel.GetActiveParts())
                {

                    if ((part.skinTemperature / part.skinMaxTemp) > _alarmHeat)
                    {
                        _vAlert.Skip("Sentence", Int32.MaxValue);
                        //If overheat, set interval to 2 seconds for repeat with brief delay
                        _interval = _heatIntervalAlert;
                        LogVa("Overheat");
                        Speech("Warning Overheat !");
                        break;
                    }
                }

                //Altitude alerts
                int previousAltitudeAlert;

                for (int i = 0; i < altitudeAlertsMeters.Length; i++)
                {
                    if (altitudeFromTerrain() > altitudeAlertsMeters[i] && altitudeFromTerrain() < altitudeAlertsMeters[i] + 2000 && altitudeAlertsAlreadySend[i] == false)
                    {
                        LogVa("Altidude alert");

                        previousAltitudeAlert = i - 1;
                        if (previousAltitudeAlert != -1)
                            altitudeAlertsAlreadySend[previousAltitudeAlert] = false;

                        altitudeAlertsAlreadySend[i] = true;

                        Speech(altitudeFromTerrain() + " meters");
                        break;
                    }
                }
                
                if (FlightGlobals.ActiveVessel.orbit.ApA > FlightGlobals.ActiveVessel.orbit.referenceBody.atmosphereDepth && apoapsisReachAlert == false)
                {
                    //Reach apoapsis for stable orbit
                    apoapsisReachAlert = true;
                    Speech("Apoapsis reach " + FlightGlobals.ActiveVessel.orbit.referenceBody.atmosphereDepth + " meters");
                }
                else if (FlightGlobals.ActiveVessel.orbit.ApA > FlightGlobals.ActiveVessel.orbit.referenceBody.atmosphereDepth && FlightGlobals.ActiveVessel.orbit.PeA > FlightGlobals.ActiveVessel.orbit.referenceBody.atmosphereDepth && orbitingReachAlert == false)
                {
                    //Reach a stable orbit
                    orbitingReachAlert = true;
                    orbitingLeaveAlert = false;
                    Speech(FlightGlobals.ActiveVessel.GetName() + " reach a stable orbit around " + FlightGlobals.ActiveVessel.orbit.referenceBody.GetName());
                }
                else if ((FlightGlobals.ActiveVessel.orbit.ApA < FlightGlobals.ActiveVessel.orbit.referenceBody.atmosphereDepth || FlightGlobals.ActiveVessel.orbit.PeA < FlightGlobals.ActiveVessel.orbit.referenceBody.atmosphereDepth) && orbitingLeaveAlert == false )
                {
                    //Leave a stable orbit
                    orbitingLeaveAlert = true;
                    orbitingReachAlert = false;
                    Speech("Warning! " + FlightGlobals.ActiveVessel.GetName() + " leave his orbit around " + FlightGlobals.ActiveVessel.orbit.referenceBody.GetName());
                }
                else if (FlightGlobals.ActiveVessel.LandedOrSplashed == true)
                {
                    UnityEngine.Debug.Log("====[VoiceAlerts]==== LANDED");
                    apoapsisReachAlert = false;
                }

                //UnityEngine.Debug.Log("====[VoiceAlerts]==== " + FlightGlobals.ActiveVessel.orbit.ApA);
                //UnityEngine.Debug.Log("====[VoiceAlerts]==== " + FlightGlobals.ActiveVessel.orbit.PeA);
                //UnityEngine.Debug.Log("====[VoiceAlerts]==== " + FlightGlobals.ActiveVessel.RevealSituationString());
                //UnityEngine.Debug.Log("====[VoiceAlerts]==== " + FlightGlobals.ActiveVessel.orbit.referenceBody.GetName());
                //UnityEngine.Debug.Log("====[VoiceAlerts]==== " + FlightGlobals.ActiveVessel.orbit.referenceBody.atmosphereDepth);
                //UnityEngine.Debug.Log("====[VoiceAlerts]==== " + FlightGlobals.ActiveVessel.situation.ToString());
            }   
        }

        /// <summary>
        /// Retrun altitude from the surface of the current body
        /// </summary>
        /// <returns></returns>
        private double altitudeFromTerrain()
        {
            return Math.Floor(FlightGlobals.ActiveVessel.altitude - FlightGlobals.ActiveVessel.pqsAltitude);
        }

        /// <summary>
        /// Display VoiceAlerts Debug
        /// </summary>
        /// <param name="args"></param>
        private void LogVa(string args)
        {
            UnityEngine.Debug.Log("====[VoiceAlerts]==== " + args);
        }
    }
}
