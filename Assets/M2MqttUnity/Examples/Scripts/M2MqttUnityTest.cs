using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using M2MqttUnity;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace M2MqttUnity.Examples
{
    public class M2MqttUnityTest : M2MqttUnityClient
    {
        public static M2MqttUnityTest Instance { get; private set; }

        public bool autoTest = false;
        
        [Header("User Interface")]
        public InputField consoleInputField;
        public Toggle encryptedToggle;
        public InputField addressInputField;
        public InputField portInputField;
        public Button connectButton;
        public Button disconnectButton;
        public Button testPublishButton;
        public Button clearButton;

        [Header("MQTT Topics")]
        public string subscribeTopic = "ar/visualizer";
        public string publishTopic = "unity/to_broker";
        
        [Header("Certificate Paths")]
        public string caCertPath = "certificates/ca.crt";
        public string clientCertPath = "certificates/client.crt";
        public string clientKeyPath = "certificates/client.key";
        
        [Header("Display Elements")]
        public Text connectionStatusText;
        public Text lastPredictionText;
        public event Action<PredictionMessage> OnPredictionReceived;

        // CHANGES
        private bool isStreamActive = false;
        private bool lastStreamState = false;

        private string handToTrigger = "FB_001";

        private const string ESP32_LEFT = "FB_001";
        private const string ESP32_RIGHT = "FB_002";

        private List<string> eventMessages = new List<string>();
        private bool updateUI = false;
        private string clientId;
        private Dictionary<string, Action<string>> topicHandlers;
        
        private const int MAX_CONSOLE_LINES = 50;
        private Queue<string> consoleHistory = new Queue<string>();

        private string lastPrediction = "";
        private float lastConfidence = 0;
        private int lastPlayer = 1;

        // UDP Discovery fields
        private const int UDP_DISCOVERY_PORT = 18883;
        private UdpClient udpListener;
        private bool brokerFound = false;
        private string discoveredBrokerIP = null;
        
        // Android multicast lock
        #if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject multicastLock;
        #endif

        // ============================================================================
        // INITIALIZATION
        // ============================================================================
        protected override void Start()
        {
            clientId = "UnityClient_" + System.Guid.NewGuid().ToString().Substring(0, 8);
            
            SetupTopicHandlers();
            
            // Removed hardcoded IP - will discover via UDP broadcast
            brokerPort = 8883;
            isEncrypted = true;
            
            if (addressInputField != null)
                addressInputField.text = "Listening for broker...";
            if (portInputField != null)
                portInputField.text = brokerPort.ToString();
            if (encryptedToggle != null)
                encryptedToggle.isOn = isEncrypted;
            
            ClearConsole();
            AddToConsole("AR Gesture System Ready.");
            AddToConsole("Listening for MQTT broker broadcasts on port 18883...");
            
            updateUI = true;
            
            // Start listening for broadcasts instead of connecting directly
            StartCoroutine(ListenForBrokerBroadcast());
        }

        // UDP Broadcast Listener
        private IEnumerator ListenForBrokerBroadcast()
        {
            // Acquire multicast lock for Android
            #if UNITY_ANDROID && !UNITY_EDITOR
            AcquireMulticastLock();
            #endif
            
            // Start listening on the discovery port
            udpListener = new UdpClient(UDP_DISCOVERY_PORT);
            udpListener.Client.ReceiveTimeout = 1000;
            
            AddToConsole("Waiting for broker broadcast...");
            AddToConsole("Make sure Python broker is running on laptop");
            
            // Listen until we find the broker
            while (!brokerFound)
            {
                // Check if there's data available
                if (udpListener.Available > 0)
                {
                    try
                    {
                        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                        byte[] data = udpListener.Receive(ref remoteEP);
                        string message = Encoding.UTF8.GetString(data);
                        
                        AddToConsole($"Received: {message}");
                        
                        // Parse the broadcast message
                        // Format: "MQTT_BROKER:172.20.10.4:8883"
                        if (message.StartsWith("MQTT_BROKER:"))
                        {
                            string[] parts = message.Split(':');
                            if (parts.Length >= 2)
                            {
                                discoveredBrokerIP = parts[1];
                                AddToConsole($"✓ Broker discovered at: {discoveredBrokerIP}");
                                brokerFound = true;
                                break;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        AddToConsole($"Error receiving broadcast: {e.Message}");
                    }
                }
                
                yield return new WaitForSeconds(0.1f);
            }
            
            // Clean up UDP listener
            udpListener.Close();
            udpListener = null;
            
            // Release multicast lock
            #if UNITY_ANDROID && !UNITY_EDITOR
            ReleaseMulticastLock();
            #endif
            
            if (!string.IsNullOrEmpty(discoveredBrokerIP))
            {
                brokerAddress = discoveredBrokerIP;
                if (addressInputField != null)
                    addressInputField.text = brokerAddress;
                
                AddToConsole($"Connecting to broker at {brokerAddress}:{brokerPort}...");
                
                // Now establish MQTT connection
                base.Start();
            }
            else
            {
                AddToConsole("Failed to discover broker. Check if Python broker is running.");
                AddToConsole("Trying fallback IP: 172.20.10.4");
                brokerAddress = "172.20.10.4";
                if (addressInputField != null)
                    addressInputField.text = brokerAddress;
                
                base.Start();
            }
        }

        #if UNITY_ANDROID && !UNITY_EDITOR
        private void AcquireMulticastLock()
        {
            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        using (AndroidJavaObject systemService = currentActivity.Call<AndroidJavaObject>("getSystemService", "wifi"))
                        {
                            AndroidJavaObject wifiManager = systemService;
                            multicastLock = wifiManager.Call<AndroidJavaObject>("createMulticastLock", "mqtt_lock");
                            multicastLock.Call("acquire");
                            AddToConsole("✓ Multicast lock acquired for UDP broadcast");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                AddToConsole($"Failed to acquire multicast lock: {e.Message}");
                AddToConsole("Try: Settings → Developer Options → Disable 'Wifi scan throttling'");
            }
        }

        private void ReleaseMulticastLock()
        {
            if (multicastLock != null)
            {
                try
                {
                    if (multicastLock.Call<bool>("isHeld"))
                    {
                        multicastLock.Call("release");
                        AddToConsole("Multicast lock released");
                    }
                }
                catch (Exception e)
                {
                    AddToConsole($"Error releasing multicast lock: {e.Message}");
                }
            }
        }
        #endif

        private void SetupTopicHandlers()
        {
            topicHandlers = new Dictionary<string, Action<string>>
            {
                { "ar/visualizer", HandleVisualizerMessage },
                { "ultra96/prediction", HandlePredictionMessage },
                { "system/status", HandleSystemStatus }
            };
        }

        // ============================================================================
        // CONSOLE DISPLAY FUNCTIONS
        // ============================================================================
        private void AddToConsole(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string formattedMessage = $"[{timestamp}] {message}";
            
            consoleHistory.Enqueue(formattedMessage);
            
            while (consoleHistory.Count > MAX_CONSOLE_LINES)
            {
                consoleHistory.Dequeue();
            }
            
            if (consoleInputField != null)
            {
                StringBuilder sb = new StringBuilder();
                foreach (string msg in consoleHistory)
                {
                    sb.AppendLine(msg);
                }
                consoleInputField.text = sb.ToString();
                consoleInputField.caretPosition = consoleInputField.text.Length;
            }
        }

        private void ClearConsole()
        {
            consoleHistory.Clear();
            if (consoleInputField != null)
            {
                consoleInputField.text = "";
            }
        }

        // ============================================================================
        // MQTT CONNECTION HANDLERS
        // ============================================================================
        protected override void OnConnecting()
        {
            base.OnConnecting();
            AddToConsole($"Connecting to broker at {brokerAddress}:{brokerPort}...");
        }

        protected override void OnConnected()
        {
            base.OnConnected();
            AddToConsole("Connected to AR Gesture broker");
            AddToConsole($"Broker: {brokerAddress}:{brokerPort}");
            AddToConsole($"Client ID: {clientId}");
            AddToConsole("Waiting for predictions...");

            SendConnectionStatus("connected");

            if (autoTest)
            {
                TestPublish();
            }

            if (connectionStatusText != null)
                connectionStatusText.text = "Connected";
            
            updateUI = true;
        }

        protected override void SubscribeTopics()
        {
            if (client == null || !client.IsConnected) return;
            
            string[] topics = new string[] { subscribeTopic, "ultra96/prediction", "system/status" };
            byte[] qosLevels = new byte[] { 
                MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE,
                MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE,
                MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE 
            };
            
            client.Subscribe(topics, qosLevels);
            AddToConsole($"Subscribed to: {subscribeTopic}, ultra96/prediction, system/status");
        }

        protected override void UnsubscribeTopics()
        {
            if (client == null || !client.IsConnected) return;
            client.Unsubscribe(new string[] { subscribeTopic, "ultra96/prediction", "system/status" });
        }

        protected override void OnConnectionFailed(string errorMessage)
        {
            AddToConsole($"Connection failed: {errorMessage}");
            if (connectionStatusText != null)
                connectionStatusText.text = "Connection Failed";
            updateUI = true;
        }

        protected override void OnDisconnected()
        {
            AddToConsole("Disconnected from broker");
            if (connectionStatusText != null)
                connectionStatusText.text = "Disconnected";
            
            isStreamActive = false;
            lastStreamState = false;
            updateUI = true;
        }

        protected override void OnConnectionLost()
        {
            AddToConsole("Connection lost");
            if (connectionStatusText != null)
                connectionStatusText.text = "Connection Lost";
            
            isStreamActive = false;
            lastStreamState = false;
            updateUI = true;
        }

        // ============================================================================
        // MESSAGE HANDLING
        // ============================================================================
        protected override void DecodeMessage(string topic, byte[] message)
        {
            string msg = System.Text.Encoding.UTF8.GetString(message);
            
            AddToConsole($"Received [{topic}]: {msg}");
            
            if (topicHandlers.ContainsKey(topic))
            {
                topicHandlers[topic](msg);
            }
        }

        private void HandleVisualizerMessage(string msg)
        {
            try
            {
                var predMsg = JsonUtility.FromJson<PredictionMessage>(msg);
                if (predMsg != null && predMsg.type == "prediction")
                {
                    lastPrediction = predMsg.prediction.ToString();
                    lastConfidence = predMsg.confidence;
                    lastPlayer = predMsg.player;
                    
                    string displayMsg = $"Prediction: {predMsg.prediction} " +
                                      $"(Conf: {predMsg.confidence:P1}) " +
                                      $"Player: {predMsg.player} " +
                                      $"ID: {predMsg.request_id}";
                    
                    AddToConsole(displayMsg);
                    Debug.Log(displayMsg);
                    
                    if (lastPredictionText != null)
                        lastPredictionText.text = displayMsg;
                    
                    OnPredictionReceived?.Invoke(predMsg);
                }
            }
            catch (Exception e)
            {
                AddToConsole($"Error parsing message: {e.Message}");
            }
        }

        private void HandlePredictionMessage(string msg)
        {
            HandleVisualizerMessage(msg);
        }

        private void HandleSystemStatus(string msg)
        {
            AddToConsole($"System Status: {msg}");
            
            if (connectionStatusText != null)
                connectionStatusText.text = "Status: " + msg;
        }

        public void SetStreamState(bool active, string hand)
        {
            isStreamActive = active;
            handToTrigger = hand;
        }

        private void SendStreamTrigger(bool start)
        {
            if (client == null || !client.IsConnected) return;

            string action = start ? "start" : "stop";
            
            var triggerMsg = new TriggerMessage
            {
                type = "trigger",
                target_device = handToTrigger,
                action = action,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            string jsonMsg = JsonUtility.ToJson(triggerMsg);
            client.Publish(publishTopic, System.Text.Encoding.UTF8.GetBytes(jsonMsg), 
                          MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, false);
            
            AddToConsole($"Stream {action} triggered for ESP32");
        }

        // HAPTIC FEEDBACK METHODS
        public void SendHapticFeedbackLeft(int duration = 500)
        {
            SendHapticFeedback(ESP32_LEFT, duration);
        }

        public void SendHapticFeedbackRight(int duration = 500)
        {
            SendHapticFeedback(ESP32_RIGHT, duration);
        }

        public void SendHapticFeedbackBoth(int duration = 500)
        {
            SendHapticFeedback(ESP32_LEFT, duration);
            SendHapticFeedback(ESP32_RIGHT, duration);
        }

        private void SendHapticFeedback(string targetDevice, int duration)
        {
            if (client == null || !client.IsConnected)
            {
                AddToConsole("Not connected - cannot send haptic feedback");
                return;
            }
            
            var hapticMsg = new HapticMessage
            {
                type = "haptic",
                target_device = targetDevice,
                duration = duration,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            string jsonMsg = JsonUtility.ToJson(hapticMsg);
            client.Publish(publishTopic, System.Text.Encoding.UTF8.GetBytes(jsonMsg), 
                          MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, false);
            
            string deviceName = targetDevice == ESP32_LEFT ? "Left" : "Right";
            AddToConsole($"Haptic feedback sent to {deviceName} ESP32 (Duration: {duration}ms)");
        }

        // ============================================================================
        // OUTGOING MESSAGES
        // ============================================================================
        private void SendConnectionStatus(string status)
        {
            if (client == null || !client.IsConnected) return;

            var connMsg = new UnityMessage
            {
                type = "connection",
                client_id = clientId,
                player = 1,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                data = $"{{\"status\":\"{status}\"}}"
            };

            string jsonMsg = JsonUtility.ToJson(connMsg);
            client.Publish(publishTopic, System.Text.Encoding.UTF8.GetBytes(jsonMsg), 
                          MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, false);
            
            AddToConsole($"Sent connection status: {status}");
        }

        public void TestPublish()
        {
            if (client == null || !client.IsConnected)
            {
                AddToConsole("Not connected to broker");
                return;
            }

            var testMsg = new UnityMessage
            {
                type = "connection",
                client_id = clientId,
                player = 1,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                data = "{\"status\":\"testing\"}"
            };

            string jsonMsg = JsonUtility.ToJson(testMsg);
            client.Publish(publishTopic, System.Text.Encoding.UTF8.GetBytes(jsonMsg), 
                          MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, false);
            
            AddToConsole("Test message sent to " + publishTopic);
        }

        // ============================================================================
        // CONNECTION CONTROL METHODS
        // ============================================================================
        public new void Disconnect()
        {
            if (isStreamActive)
            {
                SendStreamTrigger(false);
            }
            
            AddToConsole("Disconnecting...");
            base.Disconnect();
            updateUI = true;
        }

        public void OnConnectButtonClick()
        {
            Connect();
        }

        public void OnDisconnectButtonClick()
        {
            Disconnect();
        }

        public void OnTestPublishButtonClick()
        {
            TestPublish();
        }

        // ============================================================================
        // UI UPDATE METHODS
        // ============================================================================
        public void SetBrokerAddress(string brokerAddress)
        {
            if (addressInputField && !updateUI)
            {
                this.brokerAddress = brokerAddress;
            }
        }

        public void SetBrokerPort(string brokerPort)
        {
            if (portInputField && !updateUI)
            {
                int.TryParse(brokerPort, out this.brokerPort);
            }
        }

        public void SetEncrypted(bool isEncrypted)
        {
            this.isEncrypted = isEncrypted;
        }

        public void OnClearButtonClick()
        {
            ClearConsole();
            AddToConsole("Console cleared");
        }

        private void UpdateUI()
        {
            if (client == null)
            {
                if (connectButton != null)
                    connectButton.interactable = true;
                if (disconnectButton != null)
                    disconnectButton.interactable = false;
                if (testPublishButton != null)
                    testPublishButton.interactable = false;
            }
            else
            {
                bool isConnected = client.IsConnected;
                
                if (connectButton != null)
                    connectButton.interactable = !isConnected;
                if (disconnectButton != null)
                    disconnectButton.interactable = isConnected;
                if (testPublishButton != null)
                    testPublishButton.interactable = isConnected;
            }
            
            if (addressInputField != null)
                addressInputField.interactable = (client == null || !client.IsConnected);
            if (portInputField != null)
                portInputField.interactable = (client == null || !client.IsConnected);
            if (encryptedToggle != null)
                encryptedToggle.interactable = (client == null || !client.IsConnected);
            if (clearButton != null)
                clearButton.interactable = true;
                
            updateUI = false;
        }

        protected override void Update()
        {
            base.Update();

            if (updateUI)
            {
                UpdateUI();
            }
            
            if (isStreamActive != lastStreamState)
            {
                if (client != null && client.IsConnected)
                {
                    SendStreamTrigger(isStreamActive);
                }
                lastStreamState = isStreamActive;
            }
        }

        protected override void Awake()
        {
            base.Awake();

            
            
            if (Instance == null)
                Instance = this;
            else 
                Destroy(gameObject);
            
            if (isEncrypted)
            {
                try
                {
                    string streamingAssetsPath = Application.streamingAssetsPath;
                    
                    string caPath = Path.Combine(streamingAssetsPath, caCertPath);
                    string clientPath = Path.Combine(streamingAssetsPath, clientCertPath);
                    string keyPath = Path.Combine(streamingAssetsPath, clientKeyPath);
                    
                    if (File.Exists(caPath) && File.Exists(clientPath) && File.Exists(keyPath))
                    {
                        AddToConsole("TLS certificates found");
                    }
                    else
                    {
                        AddToConsole("Certificate files missing - TLS may not work");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error loading certificates: {e.Message}");
                }
            }
        }

        private void OnDestroy()
        {
            // Release multicast lock if held
            #if UNITY_ANDROID && !UNITY_EDITOR
            ReleaseMulticastLock();
            #endif
            
            if (udpListener != null)
            {
                udpListener.Close();
                udpListener = null;
            }
            
            if (client != null && client.IsConnected)
            {
                if (isStreamActive)
                {
                    SendStreamTrigger(false);
                }
                SendConnectionStatus("disconnected");
                Disconnect();
                AddToConsole("Disconnected");
            }
        }

        private void OnValidate()
        {
            if (autoTest)
            {
                autoConnect = true;
            }
        }

        // ============================================================================
        // MESSAGE CLASSES
        // ============================================================================
        [Serializable]
        public class PredictionMessage
        {
            public string type;
            public int prediction;
            public float confidence;
            public int player;
            public string request_id;
            public float total_processing_time;
            public string source;
            public long timestamp;
        }

        [Serializable]
        public class UnityMessage
        {
            public string type;
            public string client_id;
            public int player;
            public long timestamp;
            public string data;
        }
        
        [Serializable]
        public class TriggerMessage
        {
            public string type;
            public string target_device;
            public string action;
            public long timestamp;
        }

        [Serializable]
        public class HapticMessage
        {
            public string type;
            public string target_device;
            public int duration;
            public long timestamp;
        }
    }
}

