//using System;
//using System.Collections.Generic;
//using System.IO;
//using Newtonsoft.Json;
//using Serilog;

//namespace QuercusSimulator
//{
//    public static class JsonConfigManager
//    {
//        private static readonly string _filePath = "C:\\LPR\\SimulatorConfig.json"; // Original JSON file path
//        private static readonly string _cameraConfigFilePath = "C:\\LPR\\SimulatorMapping.json"; // Separate JSON file for camera info

//        // Method to get a value for a specific key from the main JSON config file
//        public static string GetValueForKey(string key)
//        {
//            try
//            {
//                if (File.Exists(_filePath))
//                {
//                    string json = File.ReadAllText(_filePath);
//                    var configValues = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

//                    if (configValues.TryGetValue(key, out string value))
//                    {
//                        return value;
//                    }
//                    else
//                    {
//                        Log.Information($"Key '{key}' not found in the JSON file.");
//                        return null;
//                    }
//                }
//                else
//                {
//                    Log.Information("JSON file does not exist. Returning null.");
//                    return null;
//                }
//            }
//            catch (Exception ex)
//            {
//                Log.Error($"Error loading JSON file: {ex.Message}");
//                // Handle the error as needed
//                return null;
//            }
//        }

//        // Method to update a value for a specific key in the main JSON config file
//        public static void UpdateValueForKey(string key, string newValue)
//        {
//            try
//            {
//                // Load existing configuration from JSON file
//                string json = File.Exists(_filePath) ? File.ReadAllText(_filePath) : "{}";
//                var configValues = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

//                // Update the value associated with the key
//                configValues[key] = newValue;

//                // Save the updated configuration back to the JSON file
//                File.WriteAllText(_filePath, JsonConvert.SerializeObject(configValues, Newtonsoft.Json.Formatting.Indented));
//            }
//            catch (Exception ex)
//            {
//                Log.Error($"Error updating JSON file: {ex.Message}");
//                // Handle the error as needed
//            }
//        }

//        // Method to get camera IP and port based on UnitId from a different JSON file
//        public static (string, int) GetCameraInfoByUnitId(uint unitId)
//        {
//            try
//            {
//                if (File.Exists(_cameraConfigFilePath))
//                {
//                    string json = File.ReadAllText(_cameraConfigFilePath);
//                    var configValues = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

//                    if (configValues.TryGetValue("RealCamMapping", out object mappingObj))
//                    {
//                        var mapping = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(mappingObj.ToString());

//                        if (mapping.TryGetValue(unitId.ToString(), out Dictionary<string, string> cameraConfig))
//                        {
//                            string ip = cameraConfig["IP"];
//                            int port = Convert.ToInt32(cameraConfig["Port"]);
//                            return (ip, port);
//                        }
//                    }
//                    Log.Information($"Camera configuration not found for UnitId: {unitId}");
//                }
//                else
//                {
//                    Log.Information("Camera configuration JSON file does not exist. Returning default IP and Port.");
//                }
//            }
//            catch (Exception ex)
//            {
//                Log.Error($"Error loading camera configuration: {ex.Message}");
//            }

//            return (null, 0); // Return null IP and 0 for port if not found
//        }
//    }
//}
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace QuercusSimulator
{
    public static class JsonConfigManager
    {
        private static readonly string _filePath = "C:\\LPR\\SimulatorConfig.json";
        private static readonly string _cameraConfigFilePath = "C:\\LPR\\SimulatorMapping.json";

        public static string GetValueForKey(string key)
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string json = File.ReadAllText(_filePath);
                    var configValues = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

                    if (configValues.TryGetValue(key, out object value))
                    {

                        return value.ToString();
                    }
                    else
                    {
                        Log.Information($"Key '{key}' not found in the JSON file.");
                        return null;
                    }
                }
                else
                {
                    Log.Information("JSON file does not exist. Returning null.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error loading JSON file: {ex.Message}");
                return null;
            }
        }

        public static void UpdateValueForKey(string key, object newValue)
        {
            try
            {
                string json = File.Exists(_filePath) ? File.ReadAllText(_filePath) : "{}";
                var configValues = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

                configValues[key] = newValue;

                File.WriteAllText(_filePath, JsonConvert.SerializeObject(configValues, Newtonsoft.Json.Formatting.Indented));
            }
            catch (Exception ex)
            {
                Log.Error($"Error updating JSON file: {ex.Message}");
            }
        }

        public static (string IP, int Port, int ReceivePort, int[] ExposureTimes) GetCameraInfoByUnitId(uint unitId)
        {
            try
            {
                if (File.Exists(_cameraConfigFilePath))
                {
                    string json = File.ReadAllText(_cameraConfigFilePath);
                    var configValues = JsonConvert.DeserializeObject<JObject>(json);

                    var mapping = configValues["RealCamMapping"];
                    var cameraConfig = mapping[unitId.ToString()];

                    if (cameraConfig != null)
                    {
                        string ip = cameraConfig["IP"].ToString();
                        int port = cameraConfig["Port"].Value<int>();
                        int receivePort = cameraConfig["ReceivePort"].Value<int>();
                        int[] exposureTimes = cameraConfig["ExposureTimes"].ToObject<int[]>();

                        return (ip, port, receivePort, exposureTimes);
                    }

                    Log.Information($"Camera configuration not found for UnitId: {unitId}");
                }
                else
                {
                    Log.Information("Camera configuration JSON file does not exist. Returning default values.");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error loading camera configuration: {ex.Message}");
            }

            return (null, 0, 0, null);
        }

        public static double GetLowBrightness()
        {
            return Convert.ToDouble(GetValueForKey("LowBrightness"));
        }

        public static double GetHighBrightness()
        {
            return Convert.ToDouble(GetValueForKey("HighBrightness"));
        }

        public static int[] GetIDs()
        {
            string idsString = GetValueForKey("IDs");
            return JsonConvert.DeserializeObject<int[]>(idsString);
        }
    }
}