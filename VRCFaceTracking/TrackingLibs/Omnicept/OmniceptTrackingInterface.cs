using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Events;
using HP.Omnicept;
using HP.Omnicept.Messaging;
using HP.Omnicept.Messaging.Messages;
using System.Threading.Tasks;
using MelonLoader;
using UnhollowerBaseLib;
using UnityEngine;
using Application = UnityEngine.Application;

namespace VRCFaceTracking.TrackingLibs.Omnicept
{
    public class OmniceptTrackingInterface : ITrackingModule
    {
        private Glia m_gliaClient;
        private GliaValueCache m_gliaValCache;
        private bool m_isConnected;

        private EyeTracking lastEyeTracking;

        private Thread _worker;
        private CancellationTokenSource token;

        public bool SupportsEye => true;
        public bool SupportsLip => false;

        private void VerifyDeadThread()
        {
            if (_worker != null)
            {
                if(_worker.IsAlive)
                    _worker.Abort();
                _worker = null;
            }
            token = new CancellationTokenSource();
        }

        private void StopGlia()
        {
            // Verify Glia is Disposed
            if(m_gliaValCache != null)
                m_gliaValCache?.Stop();
            if(m_gliaClient != null)
                m_gliaClient?.Dispose();
            m_gliaValCache = null;
            m_gliaClient = null;
            m_isConnected = false;
            Glia.cleanupNetMQConfig(true);
        }

        private bool StartGlia(bool isTesting = false)
        {
            // Verify Glia is Disposed
            StopGlia();
            // Start Glia
            bool ret = false;
            
            // Subscriptions
            MessageVersionSemantic mvs = new MessageVersionSemantic(GliaConstants.ABI_CURRENT_VERSION);
            // idk why, but when setting subList as the subscriptions, it just doesn't work
            SubscriptionList subList = new SubscriptionList()
            {
                Subscriptions =
                {
                    new Subscription(MessageTypes.ABI_MESSAGE_EYE_TRACKING, "", "", "", "", mvs),
                    new Subscription(MessageTypes.ABI_MESSAGE_EYE_PUPILLOMETRY, "", "", "", "", mvs),
                    new Subscription(MessageTypes.ABI_MESSAGE_EYE_TRACKING_FRAME, "", "", "", "", mvs)
                }
            };

            try
            {
                m_gliaClient = new Glia("VRCFaceTracking",
                    new SessionLicense(String.Empty, String.Empty, LicensingModel.Core, false));
                m_gliaValCache = new GliaValueCache(m_gliaClient.Connection);
                if(!isTesting)
                    m_gliaClient.setSubscriptions(SubscriptionList.GetSubscriptionListToAll());
                m_isConnected = true;
                ret = true;
            }
            catch (Exception e)
            {
                //MelonLogger.Error("Failed to start Glia! Exception: " + e);
                m_isConnected = false;
                ret = false;
            }
            if (!m_isConnected || isTesting)
                StopGlia();
            if (isTesting)
                return ret;
            else
                return m_isConnected;
        }

        public (bool eyeSuccess, bool lipSuccess) Initialize(bool eye, bool lip)
        {
            bool status = true;
            if (eye && SupportsEye)
                status = StartGlia(true);
            Thread.Sleep(2500);
            return (status && eye && SupportsEye, status && lip && SupportsLip);
        }

        public void StartThread()
        {
            VerifyDeadThread();
            _worker = new Thread(() =>
            {
                IL2CPP.il2cpp_thread_attach(IL2CPP.il2cpp_domain_get());
                StartGlia();
                while (!token.IsCancellationRequested)
                {
                    // Update message
                    try
                    {
                        if (m_isConnected)
                        {
                            ITransportMessage msg = RetrieveMessage();
                            if(msg != null)
                                HandleMessage(msg);
                        }
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error("Failed to get message! Exception: " + e);
                    }
                    Update();
                    Thread.Sleep(10);
                }
                StopGlia();
            });
            _worker.Start();
        }
        
        void HandleMessage(ITransportMessage msg)
        {
            switch (msg.Header.MessageType)
            {
                case MessageTypes.ABI_MESSAGE_EYE_TRACKING:
                    lastEyeTracking = m_gliaClient.Connection.Build<EyeTracking>(msg);
                    break;
            }
        }
        
        ITransportMessage RetrieveMessage()
        {
            ITransportMessage msg = null;
            if (m_gliaValCache != null)
            {
                try
                {
                    msg = m_gliaValCache.GetNext();
                }
                catch (HP.Omnicept.Errors.TransportError e)
                {
                    MelonLogger.Error(e.Message);
                }
            }
            return msg;
        }

        public struct FakeOmniceptCombinedEye
        {
            public EyeGaze Gaze;
            public float Openness;
            public float OpennessConfidence;
            //public PupilPosition Position;
            public float PupilDilation;
            public float PupilDilationConfidence;
            //public PupilPosition PupilPosition;
        }

        private FakeOmniceptCombinedEye CreateCombinedEye(HP.Omnicept.Messaging.Messages.Eye leftEye,
            HP.Omnicept.Messaging.Messages.Eye rightEye)
        {
            // BEGIN EYEGAZE
            Vector3 leftEyeGaze = new Vector3(leftEye.Gaze.X, leftEye.Gaze.Y, leftEye.Gaze.Z);
            Vector3 rightEyeGaze = new Vector3(rightEye.Gaze.X, rightEye.Gaze.Y, rightEye.Gaze.Z);
            Vector3 combinedEyeGazeCross = Vector3.Cross(leftEyeGaze, rightEyeGaze);
            // END EYEGAZE
            // BEGIN PUPILPOSITION
            /*
            Vector2 leftEyePupilPosition = new Vector2(leftEye.PupilPosition.X, leftEye.PupilPosition.Y);
            Vector2 rightEyePupilPosition = new Vector2(rightEye.PupilPosition.X, rightEye.PupilPosition.Y);
            Vector2 combinedEyePupilPosition = (leftEyePupilPosition + rightEyePupilPosition) / 2;
            */
            // END PUPILPOSITION
            // BEGIN POSITION
            /*
            Vector2 leftEyePosition = new Vector2(leftEye.Position.X, leftEye.Position.Y);
            Vector2 rightEyePosition = new Vector2(rightEye.Position.X, rightEye.Position.Y);
            Vector2 combinedEyePosition = (leftEyePosition + rightEyePosition) / 2;
            */
            // END POSITION
            return new FakeOmniceptCombinedEye()
            {
                Gaze = new EyeGaze(combinedEyeGazeCross.x, combinedEyeGazeCross.y,
                    combinedEyeGazeCross.z, (leftEye.Gaze.Confidence + rightEye.Gaze.Confidence) / 2),
                Openness = (leftEye.Openness + rightEye.Openness) / 2,
                OpennessConfidence = (leftEye.OpennessConfidence + rightEye.OpennessConfidence) / 2,
                //Position = new PupilPosition(combinedEyePupilPosition.x, combinedEyePupilPosition.y,
                    //(leftEye.PupilPosition.Confidence + rightEye.PupilPosition.Confidence) / 2),
                PupilDilation = (leftEye.PupilDilation + rightEye.PupilDilation) / 2,
                PupilDilationConfidence = (leftEye.PupilDilationConfidence + rightEye.PupilDilationConfidence) / 2//,
                //PupilPosition = new PupilPosition(combinedEyePosition.x, combinedEyePosition.y, 
                   //(leftEye.Position.Confidence + rightEye.Position.Confidence) / 2 )
            };
        }

        public void Update()
        {
            // Send latest Eye Tracking Data if it's not null
            if (lastEyeTracking != null)
            {
                //MelonLogger.Msg("Left Eye: " + lastEyeTracking.LeftEye.ToString());
                //MelonLogger.Msg("Right Eye: " + lastEyeTracking.RightEye.ToString());
                FakeOmniceptCombinedEye combinedEye = new FakeOmniceptCombinedEye();
                try
                {
                    if(lastEyeTracking.LeftEye != null && lastEyeTracking.RightEye != null)
                        combinedEye = CreateCombinedEye(lastEyeTracking.LeftEye, lastEyeTracking.RightEye);
                }
                catch (Exception e)
                {
                }

                if(lastEyeTracking.LeftEye != null && lastEyeTracking.RightEye != null)
                    UnifiedTrackingData.LatestEyeData.UpdateData(lastEyeTracking.LeftEye, lastEyeTracking.RightEye,
                        combinedEye);
            }
            else
                MelonLogger.Warning("lastEyeTracking is null!");
        }

        public void Teardown() => token.Cancel();
    }
}