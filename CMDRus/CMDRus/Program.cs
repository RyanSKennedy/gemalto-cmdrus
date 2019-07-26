using System;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Collections.Generic;
using Aladdin.HASP;
using System.IO;
using System.Text;
using System.Linq;
using MyLogClass;

namespace CMDRus
{
    public class RequestData
    {
        public HttpClient httpClient;
        public HttpResponseMessage httpClientResponse;
        public string httpClientResponseStr;
        public string httpClientResponseStatus;

        public RequestData(HttpClient newClient = null, HttpResponseMessage newResponse = null, string newResponseStr = null, string newResponseStatus = null)
        {
            httpClient = newClient;
            httpClientResponse = newResponse;
            httpClientResponseStr = newResponseStr;
            httpClientResponseStatus = newResponseStatus;
        }
    }

    class Program
    {
        private static string pKey = "";
        private static bool logIsEnabled;
        private static bool targetIsRemote;
        private static string pathForSave = "";
        private static string c2v = "";
        private static string action = "";
        public static bool logsFileIsExist;
        private static HaspStatus status;
        private static currentRequest cRequest;
        private static Dictionary<KeyValuePair<int, int>, KeyValuePair<string, string>> userCommands;
        private static Dictionary<string, int> avalibleArgs = new Dictionary<string, int>() {
            { "a", 0 },
            { "c", 0 },
            { "i", 0 },
            { "f", 0 },
            { "u", 0 },
            { "l", 2 },
            { "p", 1 },
            { "e", 1 },
            { "t", 1 },
            { "q", 4 },
            { "h", 3 }
        }; 

        private struct currentRequest
        {
            public string key;
            public string value;
        }

        static void Main(string[] args)
        {
            userCommands = new Dictionary<KeyValuePair<int, int>, KeyValuePair<string, string>>();

            #region Waiting commands from user if they not set in the beginning
            if (args == null || args.Length <= 0)
            {
                Console.WriteLine("Input: " + Environment.NewLine +
                              "a - for send activation request;" + Environment.NewLine +
                              "c - for get C2V from exist key (if more then one key in system, please set Key ID);" + Environment.NewLine +
                              "i - for get info about ProductKey;" + Environment.NewLine +
                              "f - for get Fingerprint;" + Environment.NewLine +
                              "u - for apply update;" + Environment.NewLine +
                              "l - (Optional) advanced parameter for set logs dir and logs file name;" + Environment.NewLine +
                              "p - (Optional) advanced parameter for set path for save something;" + Environment.NewLine +
                              "e - (Optional) advanced parameter for set EMS Url for activation;" + Environment.NewLine +
                              "t - (Optional) advanced parameter for set of target Key;" + Environment.NewLine +
                              "q - for Quite;" + Environment.NewLine +
                              "h - for Help." + Environment.NewLine);

                Console.WriteLine(Environment.NewLine + "/-------------------------/" + Environment.NewLine);

                action = Console.ReadLine();

                args = action.Split(" ");
            }
            #endregion

            #region Check-Correct-Split commands
            foreach (var el in args)
            {
                var elTmp = el;

                // Remove from commands char '-'
                if (el.StartsWith('-'))
                    elTmp = elTmp.Substring(1);

                // Split command on key and value (if exist)
                if (el.Contains(':'))
                {
                    var tmpSubCommand = elTmp.Split(':', 2);
                    userCommands.Add(new KeyValuePair<int, int>(userCommands.Count, 10), new KeyValuePair<string, string>(tmpSubCommand[0], tmpSubCommand[1] ));
                }
                else
                {
                    userCommands.Add(new KeyValuePair<int, int>(userCommands.Count, 10), new KeyValuePair<string, string>(elTmp, ""));
                }
            }
            #endregion

            #region Remove from user commands all not supported commands and all duplicate (stay only first setted command)
            foreach (var el in userCommands.ToList())
            {
                if (!avalibleArgs.Keys.Contains(el.Value.Key))
                    userCommands.Remove(el.Key);

                if (userCommands.Where(x => x.Value.Key == el.Value.Key).Count() > 1)
                    userCommands.Remove(userCommands.Where(x => x.Value.Key == el.Value.Key).LastOrDefault().Key);
            }
            #endregion

            #region Checking user commands on consistency
            int numberOfZeroLevelCommand = 0;
            bool hasHightLevelCommand = false;
            foreach (var el in userCommands.ToList())
            {
                foreach (var el2 in avalibleArgs)
                {
                    if (el.Value.Key == el2.Key && el2.Value == 4)
                    {
                        userCommands = new Dictionary<KeyValuePair<int, int>, KeyValuePair<string, string>>() { { new KeyValuePair<int, int>(userCommands.Count, el2.Value), new KeyValuePair<string, string>(el2.Key, "") } };
                        hasHightLevelCommand = true;
                        break;
                    }
                    else if (el.Value.Key == el2.Key && el2.Value == 3)
                    {
                        userCommands = new Dictionary<KeyValuePair<int, int>, KeyValuePair<string, string>>() { { new KeyValuePair<int, int>(userCommands.Count, el2.Value), new KeyValuePair<string, string>(el.Value.Key, el.Value.Value) } };
                        hasHightLevelCommand = true;
                        break;
                    }
                    else if (el.Value.Key == el2.Key && (el2.Value == 2 || el2.Value == 1)) 
                    {
                        var tmpData = userCommands.Where(x => x.Value.Key == el.Value.Key).First();
                        userCommands.Remove(tmpData.Key);
                        userCommands.Add(new KeyValuePair<int, int>(tmpData.Key.Key, el2.Value), tmpData.Value);
                    }
                    else if (el.Value.Key == el2.Key && el2.Value == 0)
                    {
                        var tmpData = userCommands.Where(x => x.Value.Key == el.Value.Key).First();
                        userCommands.Remove(tmpData.Key);
                        userCommands.Add(new KeyValuePair<int, int>(tmpData.Key.Key, el2.Value), tmpData.Value);
                        numberOfZeroLevelCommand++;
                    }  
                }

                if (hasHightLevelCommand)
                    break;
            }

            if (numberOfZeroLevelCommand > 1)
            {
                Console.WriteLine("Error: incompatible commands set!" + Environment.NewLine);
                userCommands = new Dictionary<KeyValuePair<int, int>, KeyValuePair<string, string>>() { { new KeyValuePair<int, int>(0, 3), new KeyValuePair<string, string>("h", "") } };
            }
            #endregion

            #region Sample's of user command:
            // 1)   a:*ProductKey* e:*EMSUrl*
            //---
            // 2)   a:*ProductKey* e:*EMSUrl* l
            // 3)   a:*ProductKey* e:*EMSUrl* l:*NameLogFile.log*
            // 4)   a:*ProductKey* e:*EMSUrl* l:*PathToLogFile\NameLogFile.log*
            //---
            // 5)   a:*ProductKey* e:*EMSUrl* t l
            // 6)   a:*ProductKey* e:*EMSUrl* t l:*NameLogFile.log*
            // 7)   a:*ProductKey* e:*EMSUrl* t l:*PathToLogFile\NameLogFile.log*
            //---
            // 8)   a:*ProductKey* e:*EMSUrl* t:*KeyID* l
            // 9)   a:*ProductKey* e:*EMSUrl* t:*KeyID* l:*NameLogFile.log*
            // 10)  a:*ProductKey* e:*EMSUrl* t:*KeyID* l:*PathToLogFile\NameLogFile.log*
            //---
            // 11)  a:*ProductKey* e:*EMSUrl* t:*KeyID* l
            // 12)  a:*ProductKey* e:*EMSUrl* t:*KeyID* l:*NameLogFile.log*
            // 13)  a:*ProductKey* e:*EMSUrl* t:*KeyID* l:*PathToLogFile\NameLogFile.log*
            //---
            // 14)  a:*ProductKey* e:*EMSUrl* t:*PathToC2VFromKey* l
            // 15)  a:*ProductKey* e:*EMSUrl* t:*PathToC2VFromKey* l:*NameLogFile.log*
            // 16)  a:*ProductKey* e:*EMSUrl* t:*PathToC2VFromKey* l:*PathToLogFile\NameLogFile.log*
            //---
            // 17)  a:*ProductKey* e:*EMSUrl* t:*PathToC2VFromKey* l
            // 18)  a:*ProductKey* e:*EMSUrl* t:*PathToC2VFromKey* l:*NameLogFile.log*
            // 19)  a:*ProductKey* e:*EMSUrl* t:*PathToC2VFromKey* l:*PathToLogFile\NameLogFile.log*
            //---
            // 20)  a:*ProductKey* e:*EMSUrl* t
            // 21)  a:*ProductKey* e:*EMSUrl* t:*KeyID* 
            // 22)  a:*ProductKey* e:*EMSUrl* t:*PathToC2VFromKey* 
            //---
            // 23)  a:*ProductKey* e:*EMSUrl* p
            // 24)  a:*ProductKey* e:*EMSUrl* p:*V2CFileName.v2c*
            // 25)  a:*ProductKey* e:*EMSUrl* p:*PathToV2CFile\V2CFileName.v2c*
            //---
            // xx)  etc "a" with "p"
            //---
            // 100) c
            // 101) c:*C2VFileName.c2v*
            // 102) c:*PathToC2VFile\C2VFileName.c2v*
            //---
            // 1xx) etc "c" with "l", "t" 
            //---
            // 200) i:*ProductKey* e:*EMSUrl*
            // 2xx) etc "i" + "e" with "l", "p"
            //---
            // 300) f
            // 301) f:*FPFileName.c2v*
            // 302) f:*PathToFPFile\FPFileName.c2v*
            //---
            // 3xx) etc "f" with "l"
            //---
            // 400) u:*UpdateFileName.v2c/h2h/h2r/alp*
            // 401) u:*PathToUpdateFile\UpdateFileName.v2c/h2h/h2r/alp*
            //---
            // 4xx) etc "u" with "l"
            //---
            // 500) q
            //---
            // 600) h
            #endregion

            #region Checking commands on integrity
            foreach (var el in userCommands.ToList())
            {
                switch(el.Value.Key)
                {
                    case "a":
                    case "i":
                        if (el.Value.Key == "")
                        {
                            userCommands = new Dictionary<KeyValuePair<int, int>, KeyValuePair<string, string>>() { { new KeyValuePair<int, int> (0, 3), new KeyValuePair<string, string>("h", "") } };
                            break;
                        }

                        if (!userCommands.ContainsKey(userCommands.Where(x => x.Value.Key == "e").First().Key) || userCommands.Where(x => x.Value.Key == "e").First().Value.Value == "")
                        {
                            userCommands = new Dictionary<KeyValuePair<int, int>, KeyValuePair<string, string>>() { { new KeyValuePair<int, int>(0, 3), new KeyValuePair<string, string>("h", "") } };
                            break;
                        }
                        break;

                    case "c":
                    case "f":
                        if (userCommands.ContainsKey(userCommands.Where(x => x.Value.Key == "e").First().Key))
                            userCommands.Remove(userCommands.Where(x => x.Value.Key == "e").First().Key);
                        break;

                    case "u":
                        if (el.Value.Key == "")
                        {
                            userCommands = new Dictionary<KeyValuePair<int, int>, KeyValuePair<string, string>>() { { new KeyValuePair<int, int>(0, 3), new KeyValuePair<string, string>("h", "") } };
                            break;
                        }

                        if (userCommands.ContainsKey(userCommands.Where(x => x.Value.Key == "e").First().Key))
                            userCommands.Remove(userCommands.Where(x => x.Value.Key == "e").First().Key);
                        break;
                }
            }
            #endregion

            #region Sorting of user commands
            var tmpUserCommandsSorted = userCommands.OrderBy(x => x.Key.Value);
            userCommands = tmpUserCommandsSorted.ToDictionary(t => t.Key, t => t.Value);
            #endregion

            #region Apply user commands
            string subReq = "";
            string tmpC2V = "";
            string savingResult = "";
            string updateStatus = "";
            string baseEMSUrl = "";
            string actXml = "<activation>" +
                               "<activationInput>" +
                                  "<activationAttribute>" +
                                     "<attributeValue>" +
                                        "<![CDATA[XXX]]>" +
                                        "</attributeValue>" +
                                     "<attributeName>C2V</attributeName>" +
                                  "</activationAttribute>" +
                                  "<comments></comments>" +
                               "</activationInput>" +
                            "</activation>";
            RequestData res = new RequestData();
            userCommands.Reverse();
            foreach (var el in userCommands)
            {
                switch (el.Value.Key)
                {
                    case "a":
                        pKey = el.Value.Value;
                        if (logIsEnabled) Log.Write("Product Key: " + pKey);
                        subReq = "productKey/" + pKey + "/activation.ws";
                        if (String.IsNullOrEmpty(tmpC2V))
                        {
                            if (logIsEnabled) Log.Write("Try to get Target...");
                            tmpC2V = GetInfo(ReturnVendorCode(), true, null);
                        }
                        if (tmpC2V.Contains("hasp_info"))
                        {
                            if (logIsEnabled) Log.Write("Taqrget C2V is: " + tmpC2V);
                            actXml = actXml.Replace("XXX", tmpC2V);
                            if (logIsEnabled) Log.Write("Activation data: " + actXml);
                            if (logIsEnabled) Log.Write("Try to activate...");
                            res = GetRequest(subReq, baseEMSUrl, HttpMethod.Post, new KeyValuePair<string, string>("activationXml", actXml));
                            if (res.httpClientResponseStatus == "OK")
                            {
                                if (logIsEnabled) Log.Write("Activation data is: " + res.httpClientResponseStr);
                                XDocument licXml = XDocument.Parse(res.httpClientResponseStr);
                                if (!targetIsRemote)
                                {
                                    if (logIsEnabled) Log.Write("Try to apply license update...");
                                    updateStatus = Update(licXml.Descendants("AID").FirstOrDefault().Value);
                                    if (updateStatus == "0")
                                        if (logIsEnabled) Log.Write("Apply license update was successfully!");
                                        else
                                        if (logIsEnabled) Log.Write("Apply update error: " + updateStatus);
                                }

                                if (!String.IsNullOrEmpty(pathForSave))
                                {
                                    if (logIsEnabled) Log.Write("Try to save license in file...");
                                    savingResult = SaveFile(pathForSave, licXml.Descendants("AID").FirstOrDefault().Value);
                                    if (logIsEnabled) Log.Write("Saving result is: " + savingResult);
                                }
                            }
                            else
                            {
                                if (logIsEnabled) Log.Write("Activation error: " + res.httpClientResponseStatus);
                            }
                        }
                        else
                        {
                            if (logIsEnabled) Log.Write("Error: " + tmpC2V);
                        }
                        break;

                    case "c":
                        if (String.IsNullOrEmpty(tmpC2V))
                        {
                            if (logIsEnabled) Log.Write("Try to get C2V...");
                            tmpC2V = GetInfo(ReturnVendorCode(), false, null);
                        }
                        if (tmpC2V.Contains("hasp_info"))
                        {
                            if (logIsEnabled) Log.Write("C2V is: " + tmpC2V);
                            c2v = tmpC2V;
                            if (logIsEnabled) Log.Write("Try to set path for saving file...");
                            pathForSave = (String.IsNullOrEmpty(pathForSave)) ? PathBuilder(el.Value.Value, el.Value.Key, el.Value.Key) : pathForSave;
                            if (logIsEnabled) Log.Write("Path for saving file: " + pathForSave);
                            if (logIsEnabled) Log.Write("Try to Save result...");
                            savingResult = SaveFile(pathForSave, c2v);
                            if (logIsEnabled) Log.Write("Result state: " + savingResult);
                        }
                        else
                        {
                            if (logIsEnabled) Log.Write("Error: " + tmpC2V);
                        }
                        break;

                    case "i":
                        pKey = el.Value.Value;
                        if (logIsEnabled) Log.Write("Product Key: " + pKey);
                        subReq = "productKey/" + pKey + ".ws";
                        if (logIsEnabled) Log.Write("Try to login in to Sentinel EMS by Product key...");
                        res = GetRequest("loginByProductKey.ws", baseEMSUrl, HttpMethod.Post, new KeyValuePair<string, string>("productKey", pKey));
                        if (res.httpClientResponseStatus == "OK")
                        {
                            if (logIsEnabled) Log.Write("Login result: " + res.httpClientResponseStatus);
                            if (logIsEnabled) Log.Write("Try to get info by Product key...");
                            res = GetRequest(subReq, baseEMSUrl, HttpMethod.Get, new KeyValuePair<string, string>("productKey", pKey), res);
                            if (res.httpClientResponseStatus == "OK")
                            {
                                if (logIsEnabled) Log.Write("Get info data: " + res.httpClientResponseStr);
                                XDocument pkInfoXml = XDocument.Parse(res.httpClientResponseStr);
                                Console.WriteLine("Product Key info: " + Environment.NewLine + pkInfoXml + Environment.NewLine);
                                if (!String.IsNullOrEmpty(pathForSave))
                                {
                                    if (logIsEnabled) Log.Write("Try to save Product key info in to file...");
                                    savingResult = SaveFile(pathForSave, res.httpClientResponseStr);
                                    if (logIsEnabled) Log.Write("Saving result is: " + savingResult);
                                }
                            }
                            else
                            {
                                if (logIsEnabled) Log.Write("Activation error: " + res.httpClientResponseStatus);
                            }
                        }
                        else
                        {
                            if (logIsEnabled) Log.Write("Login error: " + res.httpClientResponseStatus);
                        }
                        break;

                    case "f":
                        if (logIsEnabled) Log.Write("Try to get Fingerprint...");
                        c2v = GetInfo(ReturnVendorCode(), true, null);
                        if (c2v.Contains("hasp_info"))
                        {
                            if (logIsEnabled) Log.Write("Fingerprint is: " + c2v);
                            if (logIsEnabled) Log.Write("Try to set path for saving file...");
                            pathForSave = (String.IsNullOrEmpty(pathForSave)) ? PathBuilder(el.Value.Value, el.Value.Key, el.Value.Key) : pathForSave;
                            if (logIsEnabled) Log.Write("Path for saving file: " + pathForSave);
                            if (logIsEnabled) Log.Write("Try to Save result...");
                            savingResult = SaveFile(pathForSave, c2v);
                            if (logIsEnabled) Log.Write("Result state: " + savingResult);
                        }
                        else
                        {
                            if (logIsEnabled) Log.Write("Error: " + c2v);
                        }
                        break;

                    case "u":
                        if (logIsEnabled) Log.Write("Try to apply license update...");
                        updateStatus = Update(LoadFile(PathBuilder(el.Value.Value, el.Value.Key, el.Value.Key)));
                        if (updateStatus == "0")
                            if (logIsEnabled) Log.Write("Apply license update was successfully!");
                            else
                            if (logIsEnabled) Log.Write("Apply update error: " + updateStatus);
                        break;

                    case "l":
                        var setLogResult = SetLogs(PathBuilder(el.Value.Value, el.Value.Key, userCommands.LastOrDefault().Value.Key));
                        if (setLogResult == "OK") logIsEnabled = true;
                        if (logIsEnabled && setLogResult != "OK") Console.WriteLine(setLogResult + Environment.NewLine);
                        break;

                    case "p":
                        if (logIsEnabled) Log.Write("Try to set path for saving file...");
                        pathForSave = PathBuilder(el.Value.Value, el.Value.Key, userCommands.LastOrDefault().Value.Key);
                        if (logIsEnabled) Log.Write("Path for saving file: " + pathForSave);
                        break;

                    case "t":
                        if (logIsEnabled) Log.Write("Try to set Target...");
                        if (!String.IsNullOrEmpty(el.Value.Value) && el.Value.Value.Contains(Path.DirectorySeparatorChar))
                        {
                            tmpC2V = LoadFile(el.Value.Value);
                            targetIsRemote = true;
                        }
                        else if (!String.IsNullOrEmpty(el.Value.Value) && !el.Value.Value.Contains(Path.DirectorySeparatorChar))
                        {
                            tmpC2V = GetInfo(ReturnVendorCode(), false, el.Value.Value);
                        }
                        else
                        {
                            tmpC2V = GetInfo(ReturnVendorCode(), false, null);
                        }
                        break;

                    case "e":
                        if (logIsEnabled) Log.Write("Try to set Sentinel EMS URL...");
                        baseEMSUrl = el.Value.Value;
                        if (logIsEnabled) Log.Write("Sentinel EMS URL is: " + baseEMSUrl);
                        break;

                    case "q":
                        Console.WriteLine("Let's try close application..." + Environment.NewLine);
                        Environment.Exit(0);
                        return;

                    case "h":
                        Console.WriteLine("Help for CMDRus Utility." + Environment.NewLine);
                        Console.WriteLine("Supported main command: " + Environment.NewLine);
                        Console.WriteLine("\"-a:*ProductKey*\" - for send activation request;" + Environment.NewLine);
                        Console.WriteLine("\"-i:*ProductKey*\" - for get info about ProductKey;" + Environment.NewLine);
                        Console.WriteLine("\"-c\" - for get C2V from exist key." + Environment.NewLine + "       If more then one key in system, please set Key ID like: \"-c:*KeyID*\"" + Environment.NewLine);
                        Console.WriteLine("\"-f\" - for get Fingerprint from PC;" + Environment.NewLine);
                        Console.WriteLine("\"-u:*PathToUpdateFile*\" - for apply update (supported files: *.v2c, *.alp, *.h2h, *.h2r);" + Environment.NewLine);
                        Console.WriteLine("Additional(Optional) parameters: " + Environment.NewLine);
                        Console.WriteLine("\"-e:*SentinelEMSUrl*\" - for set EMS Url for activation;" + Environment.NewLine);
                        Console.WriteLine("\"-p:*PathForSave*\" - for set path for save something;" + Environment.NewLine);
                        Console.WriteLine("\"-t:*Target*\" - for set target of Key;" + Environment.NewLine);
                        Console.WriteLine("\"-l\" - for start logging and set logs dir and/or logs file name;" + Environment.NewLine);
                        Console.WriteLine("\"-h\" - for get help;" + Environment.NewLine);
                        Console.WriteLine("\"-q\" - for quite." + Environment.NewLine);
                        return;
                }
            }
            #endregion
        }

        public static string SwitchFormat(bool isNewKey)
        {
            if (isNewKey)
                return "<haspformat format=\"host_fingerprint\"/>";
            else
                return "<haspformat format=\"updateinfo\"/>";
        }

        public static string SwitchScope(string key = null)
        {
            if (String.IsNullOrEmpty(key))
                return "<?xml version=\"1.0\" encoding=\"UTF-8\" ?>" +
                        "<haspscope>" +
                        "    <license_manager hostname=\"localhost\" />" +
                        "</haspscope>";
            else
                return "<?xml version=\"1.0\" encoding=\"UTF-8\" ?>" +
                        "<haspscope>" +
                        "    <hasp id=\\" + key + "\" />" +
                        "</haspscope>" +
                        "";
        }

        public static string ReturnVendorCode()
        {
            return "AzIceaqfA1hX5wS+M8cGnYh5ceevUnOZIzJBbXFD6dgf3tBkb9cvUF/Tkd/iKu2fsg9wAysYKw7RMAsV" +
                                "vIp4KcXle/v1RaXrLVnNBJ2H2DmrbUMOZbQUFXe698qmJsqNpLXRA367xpZ54i8kC5DTXwDhfxWTOZrB" +
                                "rh5sRKHcoVLumztIQjgWh37AzmSd1bLOfUGI0xjAL9zJWO3fRaeB0NS2KlmoKaVT5Y04zZEc06waU2r6" +
                                "AU2Dc4uipJqJmObqKM+tfNKAS0rZr5IudRiC7pUwnmtaHRe5fgSI8M7yvypvm+13Wm4Gwd4VnYiZvSxf" +
                                "8ImN3ZOG9wEzfyMIlH2+rKPUVHI+igsqla0Wd9m7ZUR9vFotj1uYV0OzG7hX0+huN2E/IdgLDjbiapj1" +
                                "e2fKHrMmGFaIvI6xzzJIQJF9GiRZ7+0jNFLKSyzX/K3JAyFrIPObfwM+y+zAgE1sWcZ1YnuBhICyRHBh" +
                                "aJDKIZL8MywrEfB2yF+R3k9wFG1oN48gSLyfrfEKuB/qgNp+BeTruWUk0AwRE9XVMUuRbjpxa4YA67SK" +
                                "unFEgFGgUfHBeHJTivvUl0u4Dki1UKAT973P+nXy2O0u239If/kRpNUVhMg8kpk7s8i6Arp7l/705/bL" +
                                "Cx4kN5hHHSXIqkiG9tHdeNV8VYo5+72hgaCx3/uVoVLmtvxbOIvo120uTJbuLVTvT8KtsOlb3DxwUrwL" +
                                "zaEMoAQAFk6Q9bNipHxfkRQER4kR7IYTMzSoW5mxh3H9O8Ge5BqVeYMEW36q9wnOYfxOLNw6yQMf8f9s" +
                                "JN4KhZty02xm707S7VEfJJ1KNq7b5pP/3RjE0IKtB2gE6vAPRvRLzEohu0m7q1aUp8wAvSiqjZy7FLaT" +
                                "tLEApXYvLvz6PEJdj4TegCZugj7c8bIOEqLXmloZ6EgVnjQ7/ttys7VFITB3mazzFiyQuKf4J6+b/a/Y";
        }

        public static string GetInfo(string vCode, bool isNew = true, string keyId = null)
        {
            string info = null;
            status = Hasp.GetInfo(SwitchScope(keyId), SwitchFormat(isNew), ReturnVendorCode(), ref info);

            if (HaspStatus.StatusOk != status)
            {
                //handle error
                return status.ToString();
            }

            return info;
        }

        public static string Update(string update)
        {
            string ack = null;
            status = Hasp.Update(update, ref ack);

            return status.ToString();
        }

        public static string GetBaseDir()
        {
            // Gate path to base dir of app
            return System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
        }

        public static string LoadFile(string path)
        {
            if (!System.IO.File.Exists(path))
            {
                Console.WriteLine("Error: file: " + path + " - doesn't exist!" + Environment.NewLine);
                return null;
            }

            return System.IO.File.ReadAllText(path);
        }

        public static string SaveFile(string path, object value)
        {
            if (System.IO.File.Exists(path))
            {
                path = path.Insert(path.LastIndexOf(Path.DirectorySeparatorChar), "new_" + System.DateTime.UtcNow.Millisecond + "_");
            }

            try
            {
                using (System.IO.File.Create(path))
                {
                    try
                    {
                        System.IO.File.WriteAllText(path, value.ToString(), System.Text.Encoding.UTF8);
                        return "OK";
                    }
                    catch (Exception exl2)
                    {
                        return "Can't append data in file. Error: " + exl2.Message;
                    }
                }
            }
            catch (Exception exl1)
            {
                return "Can't create file. Error: " + exl1.Message;
            }
        }

        private static string SetLogs(string path)
        {
            // Create log file (if not exist) 
            //=============================================
            if (System.IO.File.Exists(path))
            {
                logsFileIsExist = true;
                return "OK";
            }
            else
            {
                try
                {
                    using (System.IO.File.Create(path))
                    {
                        logsFileIsExist = System.IO.File.Exists(path);
                        return "OK";
                    }
                }
                catch (Exception ex)
                {
                    return "Can't create log file. Error: " + ex.Message;
                }
            }
            //=============================================
        }

        private static string UrlBuilder(string emsUrl, string reqSubStr)
        {
            return emsUrl + ((emsUrl.Last() == '/') ? "" : "/") + reqSubStr;
        }

        private static string PathBuilder(string basePath, string subCommand, string mainCommand)
        {
            string fileName = "";

            if (String.IsNullOrEmpty(basePath))
                fileName += GetBaseDir() +
                    Path.DirectorySeparatorChar +
                    "new_" +
                    System.DateTime.UtcNow.Millisecond +
                    "_";
            else if (!basePath.Contains(Path.DirectorySeparatorChar))
                fileName += GetBaseDir() +
                    Path.DirectorySeparatorChar +
                    basePath;
            else
                fileName += basePath;

            if (String.IsNullOrEmpty(basePath) && subCommand != "l")
            {
                switch (mainCommand)
                {
                    case "a":
                        fileName += "license.v2c";
                        break;

                    case "c":
                        fileName += "c2v.c2v";
                        break;

                    case "f":
                        fileName += "fingerprint.c2v";
                        break;

                    case "i":
                        fileName += "pk_info.txt";
                        break;

                    default:
                        break;
                }
            }
            else if (String.IsNullOrEmpty(basePath) && subCommand == "l")
            {
                fileName += "app.log";
            }

            return fileName;
        }

        public static RequestData GetRequest(string rString, string rEmsUrl, HttpMethod method, KeyValuePair<string, string> rData = new KeyValuePair<string, string>(), RequestData client = null)
        {
            string fullRequestUrl = UrlBuilder(rEmsUrl, rString);
            var patterns = new[] {
                @"loginByProductKey.ws",
                @"activation/target.ws",
                @"productKey/\w{8}-\w{4}-\w{4}-\w{4}-\w{12}.ws",
                @"productKey/\w{8}-\w{4}-\w{4}-\w{4}-\w{12}/activation.ws"
            };

            Regex regex;
            cRequest = new currentRequest();

            foreach (string p in patterns)
            {
                regex = new Regex(p);

                if (regex.IsMatch(rString))
                {
                    if (p.Contains("activation.ws"))
                    {
                        cRequest.key = "activation.ws";
                        cRequest.value = rString.Split('/')[1];
                    }
                    else if (p.Contains("target.ws"))
                    {
                        cRequest.key = "target.ws";
                        cRequest.value = (rString.Contains("/")) ? rString.Split('/')[1] : rString;
                    }
                    else
                    {
                        cRequest.key = (p.Contains("/")) ? p.Split('/')[0] : p;
                        cRequest.value = (rString.Contains("/")) ? rString.Split('/')[1] : rString;
                    }
                    break;
                }
            }

            if (client == null)
            {
                client = new RequestData(null, new HttpResponseMessage(), "", "");
            }

            switch (cRequest.key)
            {
                case "loginByProductKey.ws":
                    try
                    {
                        client.httpClient = new HttpClient();

                        var content = new FormUrlEncodedContent(new[] { rData });
                        client.httpClientResponse = client.httpClient.PostAsync(fullRequestUrl, content).Result;
                        client.httpClientResponseStr = client.httpClientResponse.Content.ReadAsStringAsync().Result;
                        client.httpClientResponseStatus = client.httpClientResponse.StatusCode.ToString();
                    }
                    catch (System.AggregateException e)
                    {

                        client.httpClientResponseStatus += e.InnerException.InnerException.Message;

                    }
                    catch (HttpRequestException hE)
                    {
                        client.httpClientResponseStatus += hE.Message;
                    }
                    break;

                case "productKey":
                    if (client != null && client.httpClient != null)
                    {
                        if (client.httpClientResponseStatus == "OK")
                        {
                            try
                            {
                                if (method == HttpMethod.Get)
                                {
                                    client.httpClientResponse = client.httpClient.GetAsync(fullRequestUrl).Result;
                                }
                                else if (method == HttpMethod.Post)
                                {
                                    var content = new StringContent(rData.Value, Encoding.UTF8, "application/xml");
                                    client.httpClientResponse = client.httpClient.PostAsync(fullRequestUrl, content).Result;
                                }

                                client.httpClientResponseStr = client.httpClientResponse.Content.ReadAsStringAsync().Result;
                                client.httpClientResponseStatus = client.httpClientResponse.StatusCode.ToString();

                                if (client.httpClientResponseStatus == "OK" && method == HttpMethod.Get)
                                {
                                    XDocument tmpPKInfo = XDocument.Parse(client.httpClientResponseStr);
                                    // Here was delete something (register on new Customer)...
                                }
                            }
                            catch (System.AggregateException e)
                            {
                                client.httpClientResponseStatus += e.InnerException.InnerException.Message + " | in get info request after login by PK.";
                            }
                            catch (HttpRequestException hE)
                            {
                                client.httpClientResponseStatus += hE.Message + " | in get info request after login by PK.";
                            }
                        }
                    }
                    else
                    {
                        client.httpClientResponseStatus = "Not set HttpClient instance.";
                    }
                    break;

                case "activation.ws":
                    if (client != null && client.httpClient != null)
                    {
                        if (client.httpClientResponseStatus == "OK" || client.httpClientResponseStatus == "Created")
                        {
                            try
                            {
                                var content = new StringContent(rData.Value, Encoding.UTF8, "application/xml");
                                client.httpClientResponse = client.httpClient.PostAsync(fullRequestUrl, content).Result;
                                client.httpClientResponseStr = client.httpClientResponse.Content.ReadAsStringAsync().Result;
                                client.httpClientResponseStatus = client.httpClientResponse.StatusCode.ToString();
                            }
                            catch (System.AggregateException e)
                            {
                                client.httpClientResponseStatus += e.InnerException.InnerException.Message + " | in activate request after login by PK.";
                            }
                            catch (HttpRequestException hE)
                            {
                                client.httpClientResponseStatus += hE.Message + " | in activate request after login by PK.";
                            }
                        }
                    }
                    else
                    {
                        client.httpClientResponseStatus = "Not set HttpClient instance.";
                    }
                    break;

                case "target.ws":
                    try
                    {
                        client.httpClient = new HttpClient();

                        var content = new StringContent(rData.Value, Encoding.UTF8, "application/xml");
                        client.httpClientResponse = client.httpClient.PostAsync(fullRequestUrl, content).Result;
                        client.httpClientResponseStr = client.httpClientResponse.Content.ReadAsStringAsync().Result;
                        client.httpClientResponseStatus = client.httpClientResponse.StatusCode.ToString();
                    }
                    catch (System.AggregateException e)
                    {
                        client.httpClientResponseStatus += e.InnerException.InnerException.Message + " | in get update by C2V request.";
                    }
                    catch (HttpRequestException hE)
                    {
                        client.httpClientResponseStatus += hE.Message + " | in get update by C2V request.";
                    }
                    break;
                
                default:
                    // Передали в качестве запроса что-то невразумительное
                    client.httpClientResponseStatus = "Something whrong...";
                    break;
            }

            return client;
        }
    }
}
