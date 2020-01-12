﻿using System;
using System.Reflection;
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
using MyStaticVariable;

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
        private static bool logIsEnabled = false;
        private static bool targetIsRemote;
        private static string pathForSave = "";
        private static string pathForLog = "";
        private static string emsLogin = "";
        private static string emsPasswd = "";
        private static string emsFullUrl = "";
        private static string c2v = "";
        private static string id = "";
        private static string action = "";
        public static bool logsFileIsExist;
        public static bool globalResultIs = false;
        private static HaspStatus status;
        private static currentRequest cRequest;
        private static Dictionary<KeyValuePair<int, int>, KeyValuePair<string, string>> userCommands;
        private static Dictionary<string, int> avalibleArgs = new Dictionary<string, int>() {
            { "a", 0 },
            { "c", 0 },
            { "i", 0 },
            { "f", 0 },
            { "u", 0 },
            { "id", 0 },
            { "fetch", 0 },
            { "sync", 0 },
            { "login", 1 },
            { "passwd", 1 },
            { "l", 2 },
            { "p", 1 },
            { "e", 1 },
            { "t", 1 },
            { "q", 4 },
            { "h", 3 }
        };
        public static Log newLog;

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
                              "a   -----   for send activation request;" + Environment.NewLine +
                              "c   -----   for get C2V from exist key (if more then one key in system, please set Key ID);" + Environment.NewLine +
                              "i   -----   for get info about ProductKey;" + Environment.NewLine +
                              "f   -----   for get Fingerprint;" + Environment.NewLine +
                              "u   -----   for apply update;" + Environment.NewLine +
                              "id  -----   (In Roadmap) for get ID from PC, using for Rehosting exist SL key (should be done on aceptor PC side);" + Environment.NewLine +
                              "fetch   -   (In Roadmap) for get license update for exist key (if more then one key in system, please set Key ID);" + Environment.NewLine +
                              "sync   --   (In Roadmap) for sync current state of exist key with Sentinel EMS (if more then one key in system, please set Key ID). Should be using with parameters: \"login\"&\"passwd\";" + Environment.NewLine +
                              "login   -   (Mandatory for main command: \"sync\") advanced parameter for set account for login in Sentinel EMS;" + Environment.NewLine +
                              "passwd  -   (Mandatory for main command: \"sync\") advanced parameter for setting password for account which set by parameter \"login\";" + Environment.NewLine +
                              "l   -----   (Optional) advanced parameter for set logs dir and logs file name;" + Environment.NewLine +
                              "p   -----   (Optional) advanced parameter for set path for save something;" + Environment.NewLine +
                              "e   -----   (Optional) advanced parameter for set EMS Url;" + Environment.NewLine +
                              "t   -----   (Optional) advanced parameter for set of target Key;" + Environment.NewLine +
                              "q   -----   for Quite;" + Environment.NewLine +
                              "h   -----   for Help." + Environment.NewLine);

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
                    userCommands.Add(new KeyValuePair<int, int>(userCommands.Count, 10), new KeyValuePair<string, string>(tmpSubCommand[0], tmpSubCommand[1]));
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

            if ((numberOfZeroLevelCommand > 1 || numberOfZeroLevelCommand == 0) && hasHightLevelCommand == false)
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
            // 101) c:*KeyID*
            //---
            // 1xx) etc "c" with "l", "t" 
            //---
            // 200) i:*ProductKey* e:*EMSUrl*
            // 2xx) etc "i" + "e" with "l", "p"
            //---
            // 300) f
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
                switch (el.Value.Key)
                {
                    case "a":
                        if (userCommands.Where(x => x.Value.Key == "login").Count() > 0)
                            userCommands.Remove(userCommands.Where(x => x.Value.Key == "login").First().Key);

                        if (userCommands.Where(x => x.Value.Key == "passwd").Count() > 0)
                            userCommands.Remove(userCommands.Where(x => x.Value.Key == "passwd").First().Key);

                        if (el.Value.Value == "")
                        {
                            userCommands = new Dictionary<KeyValuePair<int, int>, KeyValuePair<string, string>>() { { new KeyValuePair<int, int>(0, 3), new KeyValuePair<string, string>("h", "") } };
                            break;
                        }

                        if (userCommands.Where(x => x.Value.Key == "e").Count() <= 0 || userCommands.Where(x => x.Value.Key == "e").First().Value.Value == "")
                        {
                            userCommands = new Dictionary<KeyValuePair<int, int>, KeyValuePair<string, string>>() { { new KeyValuePair<int, int>(0, 3), new KeyValuePair<string, string>("h", "") } };
                            break;
                        }
                        break;

                    case "i":
                        if (userCommands.Where(x => x.Value.Key == "t").Count() > 0)
                            userCommands.Remove(userCommands.Where(x => x.Value.Key == "t").First().Key);

                        if (userCommands.Where(x => x.Value.Key == "login").Count() > 0)
                            userCommands.Remove(userCommands.Where(x => x.Value.Key == "login").First().Key);

                        if (userCommands.Where(x => x.Value.Key == "passwd").Count() > 0)
                            userCommands.Remove(userCommands.Where(x => x.Value.Key == "passwd").First().Key);

                        if (el.Value.Value == "")
                        {
                            userCommands = new Dictionary<KeyValuePair<int, int>, KeyValuePair<string, string>>() { { new KeyValuePair<int, int>(0, 3), new KeyValuePair<string, string>("h", "") } };
                            break;
                        }

                        if (userCommands.Where(x => x.Value.Key == "e").Count() <= 0 || userCommands.Where(x => x.Value.Key == "e").First().Value.Value == "")
                        {
                            userCommands = new Dictionary<KeyValuePair<int, int>, KeyValuePair<string, string>>() { { new KeyValuePair<int, int>(0, 3), new KeyValuePair<string, string>("h", "") } };
                            break;
                        }
                        break;

                    case "c":
                        if (userCommands.Where(x => x.Value.Key == "e").Count() > 0)
                            userCommands.Remove(userCommands.Where(x => x.Value.Key == "e").First().Key);

                        if (userCommands.Where(x => x.Value.Key == "login").Count() > 0)
                            userCommands.Remove(userCommands.Where(x => x.Value.Key == "login").First().Key);

                        if (userCommands.Where(x => x.Value.Key == "passwd").Count() > 0)
                            userCommands.Remove(userCommands.Where(x => x.Value.Key == "passwd").First().Key);
                        break;

                    case "f":
                        if (userCommands.Where(x => x.Value.Key == "e").Count() > 0)
                            userCommands.Remove(userCommands.Where(x => x.Value.Key == "e").First().Key);

                        if (userCommands.Where(x => x.Value.Key == "t").Count() > 0)
                            userCommands.Remove(userCommands.Where(x => x.Value.Key == "t").First().Key);

                        if (userCommands.Where(x => x.Value.Key == "login").Count() > 0)
                            userCommands.Remove(userCommands.Where(x => x.Value.Key == "login").First().Key);

                        if (userCommands.Where(x => x.Value.Key == "passwd").Count() > 0)
                            userCommands.Remove(userCommands.Where(x => x.Value.Key == "passwd").First().Key);
                        break;

                    case "u":
                        if (userCommands.Where(x => x.Value.Key == "e").Count() > 0)
                            userCommands.Remove(userCommands.Where(x => x.Value.Key == "e").First().Key);

                        if (userCommands.Where(x => x.Value.Key == "p").Count() > 0)
                            userCommands.Remove(userCommands.Where(x => x.Value.Key == "p").First().Key);

                        if (userCommands.Where(x => x.Value.Key == "t").Count() > 0)
                            userCommands.Remove(userCommands.Where(x => x.Value.Key == "t").First().Key);

                        if (userCommands.Where(x => x.Value.Key == "login").Count() > 0)
                            userCommands.Remove(userCommands.Where(x => x.Value.Key == "login").First().Key);

                        if (userCommands.Where(x => x.Value.Key == "passwd").Count() > 0)
                            userCommands.Remove(userCommands.Where(x => x.Value.Key == "passwd").First().Key);

                        if (el.Value.Value == "")
                        {
                            userCommands = new Dictionary<KeyValuePair<int, int>, KeyValuePair<string, string>>() { { new KeyValuePair<int, int>(0, 3), new KeyValuePair<string, string>("h", "") } };
                            break;
                        }
                        break;

                    case "id":
                        if (userCommands.Where(x => x.Value.Key == "e").Count() > 0)
                            userCommands.Remove(userCommands.Where(x => x.Value.Key == "e").First().Key);

                        if (userCommands.Where(x => x.Value.Key == "t").Count() > 0)
                            userCommands.Remove(userCommands.Where(x => x.Value.Key == "t").First().Key);

                        if (userCommands.Where(x => x.Value.Key == "login").Count() > 0)
                            userCommands.Remove(userCommands.Where(x => x.Value.Key == "login").First().Key);

                        if (userCommands.Where(x => x.Value.Key == "passwd").Count() > 0)
                            userCommands.Remove(userCommands.Where(x => x.Value.Key == "passwd").First().Key);

                        break;

                    case "fetch":
                        if (userCommands.Where(x => x.Value.Key == "login").Count() > 0)
                            userCommands.Remove(userCommands.Where(x => x.Value.Key == "login").First().Key);

                        if (userCommands.Where(x => x.Value.Key == "passwd").Count() > 0)
                            userCommands.Remove(userCommands.Where(x => x.Value.Key == "passwd").First().Key);

                        if (userCommands.Where(x => x.Value.Key == "e").Count() <= 0 || userCommands.Where(x => x.Value.Key == "e").First().Value.Value == "")
                        {
                            userCommands = new Dictionary<KeyValuePair<int, int>, KeyValuePair<string, string>>() { { new KeyValuePair<int, int>(0, 3), new KeyValuePair<string, string>("h", "") } };
                            break;
                        }
                        break;

                    case "sync":
                        if (userCommands.Where(x => x.Value.Key == "e").Count() <= 0 || userCommands.Where(x => x.Value.Key == "e").First().Value.Value == "")
                        {
                            userCommands = new Dictionary<KeyValuePair<int, int>, KeyValuePair<string, string>>() { { new KeyValuePair<int, int>(0, 3), new KeyValuePair<string, string>("h", "") } };
                            break;
                        }

                        if (userCommands.Where(x => x.Value.Key == "login").Count() <= 0 || userCommands.Where(x => x.Value.Key == "login").First().Value.Value == "")
                        {
                            userCommands = new Dictionary<KeyValuePair<int, int>, KeyValuePair<string, string>>() { { new KeyValuePair<int, int>(0, 3), new KeyValuePair<string, string>("h", "") } };
                            break;
                        }

                        if (userCommands.Where(x => x.Value.Key == "passwd").Count() <= 0 || userCommands.Where(x => x.Value.Key == "passwd").First().Value.Value == "")
                        {
                            userCommands = new Dictionary<KeyValuePair<int, int>, KeyValuePair<string, string>>() { { new KeyValuePair<int, int>(0, 3), new KeyValuePair<string, string>("h", "") } };
                            break;
                        }
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
            string actXmlStr = "<activation>" +
                               "<activationInput>" +
                                  "<activationAttribute>" +
                                     "<attributeValue>" +
                                        "<![CDATA[XXX_C2V_XXX]]>" +
                                        "</attributeValue>" +
                                     "<attributeName>C2V</attributeName>" +
                                  "</activationAttribute>" +
                                  "<comments></comments>" +
                               "</activationInput>" +
                            "</activation>";

            string authXmlStr = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                             "<authenticationDetail>" +
                                "<userName>XXX_LOGIN_XXX</userName>" +
                                "<password>XXX_PASSWD_XXX</password>" +
                             "</authenticationDetail>";

            string protectionKeyActionXmlStr = "<ProtectionKey>" + 
                                               "<ProtectionKeyInput>" +
                                                  "<action>XXX_ACTION_XXX</action>" +
                                                  "<C2V>XXX_HASPINFO_XXX</C2V>" +
                                               "</ProtectionKeyInput>" +
                                            "</ProtectionKey>";

            RequestData res = new RequestData();
            KeyValuePair<string, string> result = new KeyValuePair<string, string>("def", "");
            foreach (var el in userCommands.Reverse())
            {
                switch (el.Value.Key)
                {
                    case "a":
                        if (logIsEnabled) Log.Write("// ----------- Start -----------");
                        if (logIsEnabled) Log.Write("Full command is: " + GetFullCommand(userCommands));
                        pKey = el.Value.Value;
                        if (logIsEnabled) Log.Write("Product Key: " + pKey);
                        Console.WriteLine("Product Key: " + pKey);
                        subReq = "productKey/" + pKey + "/activation.ws";
                        if (String.IsNullOrEmpty(tmpC2V))
                        {
                            if (logIsEnabled) Log.Write("Try to get target Fingerprint...");
                            Console.WriteLine("Try to get target Fingerprint...");
                            result = GetInfo(ReturnVendorCode(), "fp", null);
                            tmpC2V = result.Value;
                        }
                        if (tmpC2V.Contains("hasp_info"))
                        {
                            if (logIsEnabled) Log.Write("Try to login in to Sentinel EMS by Product key...");
                            Console.WriteLine("Try to login in to Sentinel EMS by Product key...");
                            res = GetRequest("loginByProductKey.ws", baseEMSUrl, HttpMethod.Post, new KeyValuePair<string, string>("productKey", pKey));
                            if (logIsEnabled) Log.Write("Sentinel EMS URL is: " + emsFullUrl);
                            Console.WriteLine("Sentinel EMS URL is: " + emsFullUrl);
                            if (res.httpClientResponseStatus == "OK")
                            {
                                actXmlStr = actXmlStr.Replace("XXX_C2V_XXX", tmpC2V);
                                XDocument resXml = XDocument.Parse(res.httpClientResponseStr);
                                XDocument actXmlStrXml = XDocument.Parse(actXmlStr);
                                if (logIsEnabled) Log.Write("Activation data is:" + Environment.NewLine + actXmlStrXml.ToString());
                                if (logIsEnabled) Log.Write("Try to activate...");
                                Console.WriteLine("Try to activate...");
                                res = GetRequest(subReq, baseEMSUrl, HttpMethod.Post, new KeyValuePair<string, string>("activationXml", actXmlStr), res);
                                if (logIsEnabled) Log.Write("Sentinel EMS URL is: " + emsFullUrl);
                                Console.WriteLine("Sentinel EMS URL is: " + emsFullUrl);
                                if (res.httpClientResponseStatus == "OK")
                                {
                                    resXml = XDocument.Parse(res.httpClientResponseStr);
                                    if (logIsEnabled) Log.Write("Activation response is:" + Environment.NewLine + resXml.ToString());
                                    if (!targetIsRemote)
                                    {
                                        if (logIsEnabled) Log.Write("Try to apply license update...");
                                        Console.WriteLine("Try to apply license update...");
                                        updateStatus = Update(resXml.Descendants("activationString").FirstOrDefault().Value).Key;
                                        if (updateStatus == "StatusOk")
                                        {
                                            if (logIsEnabled) Log.Write("Apply license update was successfully!");
                                            Console.WriteLine("Apply license update was successfully!");
                                        }
                                        else
                                        {
                                            if (logIsEnabled) Log.Write("Apply update error: " + updateStatus);
                                            Console.WriteLine("Apply update error: " + updateStatus);
                                        }
                                    }

                                    if (!String.IsNullOrEmpty(pathForSave))
                                    {
                                        if (logIsEnabled) Log.Write("Try to save license in file: " + pathForSave);
                                        Console.WriteLine("Try to save license in file: " + pathForSave);
                                        savingResult = SaveFile(pathForSave, XDocument.Parse(resXml.Descendants("activationString").FirstOrDefault().Value).ToString());
                                        if (logIsEnabled) Log.Write("Saving result is: " + savingResult);
                                        Console.WriteLine("Saving result is: " + savingResult);
                                    }

                                    globalResultIs = true;
                                }
                                else
                                {
                                    if (logIsEnabled) Log.Write("Activation error: " + res.httpClientResponseStatus + " - " + res.httpClientResponseStr);
                                    Console.WriteLine("Activation error: " + res.httpClientResponseStatus + " - " + res.httpClientResponseStr);
                                }
                            }
                            else
                            {
                                if (logIsEnabled) Log.Write("Login error: " + res.httpClientResponseStatus + " - " + res.httpClientResponseStr);
                                Console.WriteLine("Login error: " + res.httpClientResponseStatus + " - " + res.httpClientResponseStr);
                            }
                        }
                        else
                        {
                            if (logIsEnabled) Log.Write("Error: " + (result.Key != "def" ? result.Key : tmpC2V));
                            Console.WriteLine("Error: " + (result.Key != "def" ? result.Key : tmpC2V));
                        }
                        break;

                    case "c":
                        if (logIsEnabled) Log.Write("// ----------- Start -----------");
                        if (logIsEnabled) Log.Write("Full command is: " + GetFullCommand(userCommands));
                        if (String.IsNullOrEmpty(tmpC2V))
                        {
                            if (logIsEnabled) Log.Write("Try to get target C2V...");
                            Console.WriteLine("Try to get target C2V...");
                            result = GetInfo(ReturnVendorCode(), "c2v", null);
                            tmpC2V = result.Value;
                        }
                        if (tmpC2V.Contains("hasp_info"))
                        {
                            XDocument tmpC2vXml = XDocument.Parse(tmpC2V);
                            if (logIsEnabled) Log.Write("C2V is: " + Environment.NewLine + (String.IsNullOrWhiteSpace(tmpC2vXml.ToString().Substring(tmpC2vXml.ToString().Length - 1)) ? tmpC2vXml.ToString().Remove(tmpC2vXml.ToString().Length - 1) : tmpC2vXml.ToString()));
                            c2v = tmpC2V;
                            if (logIsEnabled) Log.Write("Try to save C2V in file: " + pathForSave);
                            Console.WriteLine("Try to save C2V in file: " + pathForSave);
                            savingResult = SaveFile(pathForSave, c2v);
                            if (logIsEnabled) Log.Write("Result state: " + savingResult);
                            Console.WriteLine("Result state: " + savingResult);

                            globalResultIs = true;
                        }
                        else
                        {
                            if (logIsEnabled) Log.Write("Error: " + (result.Key != "def" ? result.Key : tmpC2V));
                            Console.WriteLine("Error: " + (result.Key != "def" ? result.Key : tmpC2V) + Environment.NewLine);
                        }
                        break;

                    case "i":
                        if (logIsEnabled) Log.Write("// ----------- Start -----------");
                        if (logIsEnabled) Log.Write("Full command is: " + GetFullCommand(userCommands));
                        pKey = el.Value.Value;
                        if (logIsEnabled) Log.Write("Product Key: " + pKey);
                        Console.WriteLine("Product Key: " + pKey);
                        subReq = "productKey/" + pKey + ".ws";
                        if (logIsEnabled) Log.Write("Try to login in to Sentinel EMS by Product key...");
                        Console.WriteLine("Try to login in to Sentinel EMS by Product key...");
                        res = GetRequest("loginByProductKey.ws", baseEMSUrl, HttpMethod.Post, new KeyValuePair<string, string>("productKey", pKey));
                        if (logIsEnabled) Log.Write("Sentinel EMS URL is: " + emsFullUrl);
                        Console.WriteLine("Sentinel EMS URL is: " + emsFullUrl);
                        if (res.httpClientResponseStatus == "OK")
                        {
                            if (logIsEnabled) Log.Write("Login result: " + res.httpClientResponseStatus);
                            Console.WriteLine("Login result: " + res.httpClientResponseStatus);
                            if (logIsEnabled) Log.Write("Try to get info by Product key...");
                            Console.WriteLine("Try to get info by Product key...");
                            res = GetRequest(subReq, baseEMSUrl, HttpMethod.Get, new KeyValuePair<string, string>("productKey", pKey), res);
                            if (logIsEnabled) Log.Write("Sentinel EMS URL is: " + emsFullUrl);
                            Console.WriteLine("Sentinel EMS URL is: " + emsFullUrl);
                            if (res.httpClientResponseStatus == "OK")
                            {
                                XDocument resXml = XDocument.Parse(res.httpClientResponseStr);
                                if (logIsEnabled) Log.Write("Get info result: " + res.httpClientResponseStatus);
                                Console.WriteLine("Get info result: " + res.httpClientResponseStatus);
                                if (logIsEnabled) Log.Write("Get info data: " + Environment.NewLine + resXml.ToString());
                                if (!String.IsNullOrEmpty(pathForSave))
                                {
                                    if (logIsEnabled) Log.Write("Try to save Product key info in file: " + pathForSave);
                                    Console.WriteLine("Try to save Product key info in file: " + pathForSave);
                                    savingResult = SaveFile(pathForSave, resXml.ToString());
                                    if (logIsEnabled) Log.Write("Saving result is: " + savingResult);
                                    Console.WriteLine("Saving result is: " + savingResult);
                                }

                                globalResultIs = true;
                            }
                            else
                            {
                                if (logIsEnabled) Log.Write("PK get info error: " + res.httpClientResponseStatus + " - " + res.httpClientResponseStr);
                                Console.WriteLine("PK get info error: " + res.httpClientResponseStatus + " - " + res.httpClientResponseStr);
                            }
                        }
                        else
                        {
                            if (logIsEnabled) Log.Write("Login error: " + res.httpClientResponseStatus + " - " + res.httpClientResponseStr);
                            Console.WriteLine("Login error: " + res.httpClientResponseStatus + " - " + res.httpClientResponseStr);
                        }
                        break;

                    case "f":
                        if (logIsEnabled) Log.Write("// ----------- Start -----------");
                        if (logIsEnabled) Log.Write("Full command is: " + GetFullCommand(userCommands));
                        if (logIsEnabled) Log.Write("Try to get Fingerprint...");
                        Console.WriteLine("Try to get Fingerprint...");
                        result = GetInfo(ReturnVendorCode(), "fp", null);
                        c2v = result.Value;
                        XDocument c2vXml = XDocument.Parse(c2v);
                        if (c2v.Contains("hasp_info"))
                        {
                            if (logIsEnabled) Log.Write("Fingerprint is: " + Environment.NewLine + (String.IsNullOrWhiteSpace(c2vXml.ToString().Substring(c2vXml.ToString().Length - 1)) ? c2vXml.ToString().Remove(c2vXml.ToString().Length - 1) : c2vXml.ToString()));
                            if (logIsEnabled) Log.Write("Try to save Fingerprint in file: " + pathForSave);
                            Console.WriteLine("Try to save Fingerprint in file: " + pathForSave);
                            savingResult = SaveFile(pathForSave, c2v);
                            if (logIsEnabled) Log.Write("Result state: " + savingResult);
                            Console.WriteLine("Result state: " + savingResult);

                            globalResultIs = true;
                        }
                        else
                        {
                            if (logIsEnabled) Log.Write("Error: " + result.Key);
                            Console.WriteLine("Error: " + result.Key + Environment.NewLine);
                        }
                        break;

                    case "u":
                        if (logIsEnabled) Log.Write("// ----------- Start -----------");
                        if (logIsEnabled) Log.Write("Full command is: " + GetFullCommand(userCommands));
                        if (logIsEnabled) Log.Write("Try to apply license update...");
                        Console.WriteLine("Try to apply license update...");
                        result = Update(LoadFile(PathBuilder(el.Value.Value, el.Value.Key, el.Value.Key)));
                        updateStatus = result.Key;
                        if (updateStatus == "StatusOk")
                        {
                            if (logIsEnabled) Log.Write("Apply license update was successfully!");
                            Console.WriteLine("Apply license update was successfully!");

                            globalResultIs = true;
                        }
                        else
                        {
                            if (logIsEnabled) Log.Write("Apply update error: " + updateStatus);
                            Console.WriteLine("Apply update error: " + updateStatus);
                        }
                        break;

                    case "id":
                        if (logIsEnabled) Log.Write("// ----------- Start -----------");
                        if (logIsEnabled) Log.Write("Full command is: " + GetFullCommand(userCommands));
                        if (logIsEnabled) Log.Write("Try to get ID file...");
                        Console.WriteLine("Try to get ID file...");
                        result = GetInfo(ReturnVendorCode(), "id", null);
                        id = result.Value;
                        XDocument idXml = XDocument.Parse(id);
                        if (!String.IsNullOrEmpty(id))
                        {
                            if (logIsEnabled) Log.Write("File ID is: " + Environment.NewLine + idXml.ToString());
                            if (logIsEnabled) Log.Write("Try to save ID in file: " + pathForSave);
                            Console.WriteLine("Try to save ID in file: " + pathForSave);
                            savingResult = SaveFile(pathForSave, id);
                            if (logIsEnabled) Log.Write("Result state: " + savingResult);
                            Console.WriteLine("Result state: " + savingResult);

                            globalResultIs = true;
                        }
                        else
                        {
                            if (logIsEnabled) Log.Write("Error: " + result.Key);
                            Console.WriteLine("Error: " + result.Key + Environment.NewLine);
                        }
                        break;

                    case "fetch":
                        if (logIsEnabled) Log.Write("// ----------- Start -----------");
                        if (logIsEnabled) Log.Write("Full command is: " + GetFullCommand(userCommands));
                        if (String.IsNullOrEmpty(tmpC2V))
                        {
                            if (logIsEnabled) Log.Write("Try to get Target...");
                            Console.WriteLine("Try to get Target...");
                            result = GetInfo(ReturnVendorCode(), "c2v", null);
                            tmpC2V = result.Value;
                        }
                        if (tmpC2V.Contains("hasp_info"))
                        {
                            if (logIsEnabled) Log.Write("Target C2V is: " + Environment.NewLine + (String.IsNullOrWhiteSpace(tmpC2V.Substring(tmpC2V.Length - 1)) ? tmpC2V.Remove(tmpC2V.Length - 1) : tmpC2V));
                            if (logIsEnabled) Log.Write("Try to get pending updates for exist key...");
                            Console.WriteLine("Try to get pending updates for exist key...");
                            res = GetRequest("activation/target.ws", baseEMSUrl, HttpMethod.Post, new KeyValuePair<string, string>("c2v", tmpC2V), res);
                            if (logIsEnabled) Log.Write("Sentinel EMS URL is: " + emsFullUrl);
                            Console.WriteLine("Sentinel EMS URL is: " + emsFullUrl);
                            XDocument resXml = XDocument.Parse(res.httpClientResponseStr);
                            if (res.httpClientResponseStatus == "OK")
                            {
                                if (!res.httpClientResponseStr.Contains("No pending update"))
                                {
                                    if (logIsEnabled) Log.Write("Update data is: " + Environment.NewLine + resXml.ToString());

                                    if ((userCommands.Where(x => x.Value.Key == "t").Count() == 0) || ((userCommands.Where(x => x.Value.Key == "t").Count() > 0) && (!String.IsNullOrEmpty(userCommands.Where(x => x.Value.Key == "t").FirstOrDefault().Value.Value) && (!userCommands.Where(x => x.Value.Key == "t").FirstOrDefault().Value.Value.Contains(Path.DirectorySeparatorChar)))))
                                    {
                                        if (logIsEnabled) Log.Write("Try to apply pending updates...");
                                        Console.WriteLine("Try to apply pending updates...");
                                        result = Update(res.httpClientResponseStr);
                                        updateStatus = result.Key;
                                        if (updateStatus == "StatusOk")
                                        {
                                            if (logIsEnabled) Log.Write("Apply license update was successfully!");
                                            Console.WriteLine("Apply license update was successfully!");

                                            globalResultIs = true;
                                        }
                                        else
                                        {
                                            if (logIsEnabled) Log.Write("Apply update error: " + updateStatus);
                                            Console.WriteLine("Apply update error: " + updateStatus);
                                        }
                                    }

                                    if (!String.IsNullOrEmpty(pathForSave))
                                    {
                                        if (logIsEnabled) Log.Write("Try to save Pending updates in file: " + pathForSave);
                                        Console.WriteLine("Try to save Pending updates in file: " + pathForSave);
                                        savingResult = SaveFile(pathForSave, resXml.ToString());
                                        if (logIsEnabled) Log.Write("Saving result is: " + savingResult);
                                        Console.WriteLine("Saving result is: " + savingResult);
                                    }
                                }
                                else
                                {
                                    if (logIsEnabled) Log.Write("Fetch pending update response is: " + res.httpClientResponseStr);
                                    Console.WriteLine("Fetch pending update response is: " + res.httpClientResponseStr);
                                }

                                globalResultIs = true;
                            }
                            else
                            {
                                if (logIsEnabled) Log.Write("Request error: " + res.httpClientResponseStatus + " - " + res.httpClientResponseStr);
                                Console.WriteLine("Request error: " + res.httpClientResponseStatus + " - " + res.httpClientResponseStr);
                            }
                        }
                        else
                        {
                            if (logIsEnabled) Log.Write("Error: " + (result.Key != "def" ? result.Key : tmpC2V));
                            Console.WriteLine("Error: " + (result.Key != "def" ? result.Key : tmpC2V));
                        }
                        break;

                    case "sync":
                        if (logIsEnabled) Log.Write("// ----------- Start -----------");
                        if (logIsEnabled) Log.Write("Full command is: " + GetFullCommand(userCommands));
                        if (String.IsNullOrEmpty(tmpC2V))
                        {
                            if (logIsEnabled) Log.Write("Try to get Target...");
                            Console.WriteLine("Try to get Target...");
                            result = GetInfo(ReturnVendorCode(), "c2v", null);
                            tmpC2V = result.Value;
                        }
                        if (tmpC2V.Contains("hasp_info"))
                        {
                            authXmlStr = authXmlStr.Replace("XXX_LOGIN_XXX", emsLogin);
                            authXmlStr = authXmlStr.Replace("XXX_PASSWD_XXX", emsPasswd);
                            if (logIsEnabled) Log.Write("Try to login in to Sentinel EMS by login: " + emsLogin + " & password: " + emsPasswd + "...");
                            Console.WriteLine("Try to login in to Sentinel EMS by login: " + emsLogin + " & password: " + emsPasswd + "...");
                            res = GetRequest("login.ws", baseEMSUrl, HttpMethod.Post, new KeyValuePair<string, string>("authenticationDetail", authXmlStr));
                            if (logIsEnabled) Log.Write("Sentinel EMS URL is: " + emsFullUrl);
                            Console.WriteLine("Sentinel EMS URL is: " + emsFullUrl);
                            if (res.httpClientResponseStatus == "OK")
                            {
                                c2vXml = XDocument.Parse(tmpC2V);
                                if (logIsEnabled) Log.Write("Target C2V is: " + Environment.NewLine + (String.IsNullOrWhiteSpace(c2vXml.ToString().Substring(c2vXml.ToString().Length - 1)) ? c2vXml.ToString().Remove(c2vXml.ToString().Length - 1) : c2vXml.ToString()));
                                if (logIsEnabled) Log.Write("Try to sync current state of exist key with Sentinel EMS database...");
                                Console.WriteLine("Try to sync current state of exist key with Sentinel EMS database...");
                                
                                protectionKeyActionXmlStr = protectionKeyActionXmlStr.Replace("XXX_HASPINFO_XXX", c2vXml.ToString());
                                protectionKeyActionXmlStr = protectionKeyActionXmlStr.Replace("XXX_ACTION_XXX", "Checkin");

                                res = GetRequest("target.ws", baseEMSUrl, HttpMethod.Post, new KeyValuePair<string, string>("Checkin", protectionKeyActionXmlStr), res);
                                if (logIsEnabled) Log.Write("Sentinel EMS URL is: " + emsFullUrl);
                                Console.WriteLine("Sentinel EMS URL is: " + emsFullUrl);
                                if (res.httpClientResponseStatus == "OK")
                                {
                                    XDocument tmpResXml = XDocument.Parse(res.httpClientResponseStr);
                                    if (logIsEnabled) Log.Write("Sync request response is: " + Environment.NewLine + tmpResXml.ToString());

                                    if (!String.IsNullOrEmpty(pathForSave))
                                    {
                                        if (logIsEnabled) Log.Write("Try to save Response info in file: " + pathForSave);
                                        Console.WriteLine("Try to save Response info in file: " + pathForSave);
                                        savingResult = SaveFile(pathForSave, tmpResXml.ToString());
                                        if (logIsEnabled) Log.Write("Saving result is: " + savingResult);
                                        Console.WriteLine("Saving result is: " + savingResult);
                                    }

                                    globalResultIs = true;
                                }
                                else
                                {
                                    if (logIsEnabled) Log.Write("Sync request error: " + res.httpClientResponseStatus + " - " + res.httpClientResponseStr);
                                    Console.WriteLine("Sync request error: " + res.httpClientResponseStatus + " - " + res.httpClientResponseStr);
                                }
                            }
                            else
                            {
                                if (logIsEnabled) Log.Write("Login error: " + res.httpClientResponseStatus + " - " + res.httpClientResponseStr);
                                Console.WriteLine("Login error: " + res.httpClientResponseStatus + " - " + res.httpClientResponseStr);
                            }
                        }
                        else
                        {
                            if (logIsEnabled) Log.Write("Error: " + (result.Key != "def" ? result.Key : tmpC2V));
                            Console.WriteLine("Error: " + (result.Key != "def" ? result.Key : tmpC2V));
                        }
                        break;

                    case "login":
                        emsLogin = el.Value.Value;
                        break;

                    case "passwd":
                        emsPasswd = el.Value.Value;
                        break;

                    case "l":
                        pathForLog = PathBuilder(el.Value.Value, el.Value.Key, userCommands.LastOrDefault().Value.Key);
                        var setLogResult = SetLogs(pathForLog);
                        if (setLogResult == "OK")
                        {
                            logIsEnabled = true;
                            newLog = new Log(pathForLog);
                        }
                        Console.WriteLine("Set logs status is: " + setLogResult);
                        Console.WriteLine("Path for saving Logs file is: " + pathForLog);
                        break;

                    case "p":
                        pathForSave = PathBuilder(el.Value.Value, el.Value.Key, userCommands.FirstOrDefault().Value.Key);
                        break;

                    case "t":
                        if (!String.IsNullOrEmpty(el.Value.Value) && el.Value.Value.Contains(Path.DirectorySeparatorChar))
                        {
                            tmpC2V = LoadFile(el.Value.Value);
                            targetIsRemote = true;
                        }
                        else if (!String.IsNullOrEmpty(el.Value.Value) && !el.Value.Value.Contains(Path.DirectorySeparatorChar))
                        {
                            tmpC2V = GetInfo(ReturnVendorCode(), "c2v", el.Value.Value).Value;
                        }
                        else
                        {
                            tmpC2V = GetInfo(ReturnVendorCode(), "c2v", null).Value;
                        }
                        break;

                    case "e":
                        baseEMSUrl = el.Value.Value;
                        break;

                    case "q":
                        Console.WriteLine("Let's try to close application..." + Environment.NewLine);
                        Environment.Exit(0);
                        return;

                    case "h":
                        Console.WriteLine("Help for CMDRus Utility." + Environment.NewLine);
                        Console.WriteLine("Version: " + Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion + Environment.NewLine);
                        Console.WriteLine("Main parameters: " + Environment.NewLine);
                        Console.WriteLine("-a:<ProductKey>          Send activation request." + Environment.NewLine);
                        Console.WriteLine("-i:<ProductKey>          Get info about ProductKey." + Environment.NewLine);
                        Console.WriteLine("-c                       Get C2V from exist key. If more then one key in system," + Environment.NewLine +
                                          "                         please set Key ID like: -t:<KeyID>" + Environment.NewLine);
                        Console.WriteLine("-f                       Get Fingerprint from PC." + Environment.NewLine);
                        Console.WriteLine("-u:<PathToUpdateFile>    Apply update (supported files: *.v2c, *.v2cp," + Environment.NewLine +
                                          "                         *.alp, *.h2h, *.h2r)." + Environment.NewLine);
                        Console.WriteLine("-id                      Get *.id from PC, using for Rehost." + Environment.NewLine);
                        Console.WriteLine("-fetch                   Send request for check required update for existed key." + Environment.NewLine +
                                          "                         If more then one key in system, please set Key ID like:" + Environment.NewLine +
                                          "                         -t:<KeyID>" + Environment.NewLine);
                        Console.WriteLine("-sync                    Send request with C2V for sync current state of the key" + Environment.NewLine +
                                          "                         with Sentinel EMS. If more then one key in system," + Environment.NewLine +
                                          "                         please set Key ID like: -t:<KeyID>" + Environment.NewLine);
                        Console.WriteLine("Additional(Mandatory for \"sync\") parameters: " + Environment.NewLine);
                        Console.WriteLine("-login                   For set account name from Sentinel EMS, will be using" + Environment.NewLine);
                        Console.WriteLine("                         for login in vendor part of Sentinel EMS." + Environment.NewLine);
                        Console.WriteLine("                         !Be careful!: account should be with minimun rights!" + Environment.NewLine);
                        Console.WriteLine("-passwd                  For set password from account which was set in" + Environment.NewLine);
                        Console.WriteLine("                         \"login\" parameter." + Environment.NewLine);
                        Console.WriteLine("Additional(Optional) parameters: " + Environment.NewLine);
                        Console.WriteLine("-e:<SentinelEMSUrl>      Set EMS Url for activation. Mandatory for:" + Environment.NewLine +
                                          "                         \"-a\", \"-i\", \"-fetch\" & \"-sync\"." + Environment.NewLine);
                        Console.WriteLine("-p:<PathForSave>         Set path for save something. Mandatory for:" + Environment.NewLine +
                                          "                         \"-c\", \"-f\" & \"-id\"." + Environment.NewLine);
                        Console.WriteLine("-t:<Target>              Set target of Key." + Environment.NewLine);
                        Console.WriteLine("-l                       Start logging and set logs dir and/or logs file name." + Environment.NewLine);
                        Console.WriteLine("-h                       Get help." + Environment.NewLine);
                        Console.WriteLine("-q                       Quite." + Environment.NewLine);
                        Console.WriteLine(Environment.NewLine);
                        Console.WriteLine("Sample's: " + Environment.NewLine);
                        Console.WriteLine("1) dotnet cmdrus.dll -a:<ProductKey> -e:http://emsurl:8080/ems -p:License.v2c -t:<KeyIdForUpdate> -l:LogFilePath" + Path.DirectorySeparatorChar + "LogFile.log" + Environment.NewLine);
                        Console.WriteLine("2) dotnet cmdrus.dll -a:<ProductKey> -e:http://emsurl:8080/ems -p:License.v2c -t:KeyStatePath" + Path.DirectorySeparatorChar + "KeyState.c2v -l:LogFilePath" + Path.DirectorySeparatorChar + "LogFile.log" + Environment.NewLine);
                        Console.WriteLine("3) dotnet cmdrus.dll -a:<ProductKey> -e:http://emsurl:8080/ems -p:License.v2c -l:LogFilePath" + Path.DirectorySeparatorChar + "LogFile.log" + Environment.NewLine);
                        Console.WriteLine("4) dotnet cmdrus.dll -i:<ProductKey> -e:http://emsurl:8080/ems -p:KeyInfo.txt -l:LogFile.log" + Environment.NewLine);
                        Console.WriteLine("5) dotnet cmdrus.dll -c -t:<KeyId> -p:KeyState.c2v -l:LogFile.log" + Environment.NewLine);
                        Console.WriteLine("6) dotnet cmdrus.dll -c -p:KeyStatePath" + Path.DirectorySeparatorChar + "KeyState.c2v -l" + Environment.NewLine);
                        Console.WriteLine("7) dotnet cmdrus.dll -f -p:FingerPrint.c2v -l" + Environment.NewLine);
                        Console.WriteLine("8) dotnet cmdrus.dll -u:LicenseFilePath" + Path.DirectorySeparatorChar + "LicenseFile.v2c -l" + Environment.NewLine);
                        Console.WriteLine("9) dotnet cmdrus.dll -id -p:DeviceID.id -l" + Environment.NewLine);
                        Console.WriteLine("10) dotnet cmdrus.dll -fetch -e:http://emsurl:8080/ems -t:<KeyId> -p:PendingUpdates.v2cp -l" + Environment.NewLine);
                        Console.WriteLine("11) dotnet cmdrus.dll -sync -login:<SentinelEmsAccount> -passwd:<PasswordFromEmsAccount> -e:http://emsurl:8080/ems -t:<KeyId> -l" + Environment.NewLine);

                        return;
                }
            }
            #endregion

            if (globalResultIs)
            {
                if (logIsEnabled) Log.Write("Global result: Successfully!");
                Console.WriteLine("Global result: Successfully!" + Environment.NewLine);
            }
            else
            {
                if (logIsEnabled) Log.Write("Global result: BadRequest!");
                Console.WriteLine("Global result: BadRequest!" + Environment.NewLine);
            }

            if (logIsEnabled) Log.Write("// ----------- End -----------");

            // Just for see results in console
            if (true) Console.ReadLine();
        }

        public static string SwitchFormat(string action)
        {
            switch (action)
            {
                case "fp":
                    return "<haspformat format=\"host_fingerprint\"/>";

                case "c2v":
                    return "<haspformat format=\"updateinfo\"/>";

                case "id":
                    return "<haspformat root=\"location\">" + 
                           "    <license_manager>" + 
                           "        <attribute name=\"id\"/>" + 
                           "        <attribute name=\"time\"/>" + 
                           "        <element name=\"hostname\"/>" +
                           "        <element name=\"version\"/>" + 
                           "        <element name=\"host_fingerprint\"/>" +
                           "    </license_manager>" +
                           "</haspformat >";

                default:
                    return "";
            }
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
                        "    <hasp id=\"" + key + "\" />" +
                        "</haspscope>" +
                        "";
        }

        public static string ReturnVendorCode()
        {
            return MyStaticVariable.MyStaticVariable.vendorCode["DEMOMA"];
        }

        public static KeyValuePair<string, string> GetInfo(string vCode, string action, string keyId = null)
        {
            string info = null;
            status = Hasp.GetInfo(SwitchScope(keyId), SwitchFormat(action), vCode, ref info);

            return new KeyValuePair<string, string>(status.ToString(), info);
        }

        public static KeyValuePair<string, string> Update(string update)
        {
            string ack = null;
            status = Hasp.Update(update, ref ack);

            return new KeyValuePair<string, string>(status.ToString(), ack);
        }

        public static string GetBaseDir()
        {
            // Gate path to base dir of app
            //=============================================
            return System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            //=============================================
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
                path = path.Insert(path.LastIndexOf(Path.DirectorySeparatorChar), "new_" +
                    string.Format("{0:dd-MM-yyyy_HH-mm-ss-fff}", DateTime.Now) +
                    "_");
            }

            try
            {
                System.IO.File.WriteAllText(path, value.ToString(), System.Text.Encoding.UTF8);
                return "OK";
            }
            catch (Exception ex)
            {
                return "Can't create or append data in file. Error: " + ex.Message;
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
            string fullEmsUrl = emsUrl;
            fullEmsUrl += ((fullEmsUrl.Last() == '/') ? "" : "/");
            fullEmsUrl += (!fullEmsUrl.Contains("ems") ? "ems" : "");
            fullEmsUrl += ((fullEmsUrl.Last() == '/') ? "" : "/");
            fullEmsUrl += (!fullEmsUrl.Contains("v710") ? "v710" : "");
            fullEmsUrl += ((fullEmsUrl.Last() == '/') ? "" : "/");
            fullEmsUrl += (!fullEmsUrl.Contains("ws") ? "ws" : "");
            fullEmsUrl += ((fullEmsUrl.Last() == '/') ? "" : "/");
            fullEmsUrl += reqSubStr;

            emsFullUrl = fullEmsUrl;

            return fullEmsUrl;
        }

        private static string PathBuilder(string basePath, string subCommand, string mainCommand)
        {
            string fileName = "";

            if (String.IsNullOrEmpty(basePath))
                fileName += GetBaseDir() +
                    Path.DirectorySeparatorChar +
                    "new_" +
                    string.Format("{0:dd-MM-yyyy_HH-mm-ss-fff}", DateTime.Now) + 
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

                    case "id":
                        fileName += "device_id.id";
                        break;

                    case "fetch":
                        fileName += "pending_updates.v2cp";
                        break;

                    case "sync":
                        fileName += "current_state.xml";
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

        public static string GetFullCommand(Dictionary<KeyValuePair<int, int>, KeyValuePair<string, string>> sourceData)
        {
            string fullCommand = "CMDRus.dll ";
            foreach (var el in sourceData.Reverse())
            {
                fullCommand += "-" + el.Value.Key + (!String.IsNullOrEmpty(el.Value.Value) ? (":" + el.Value.Value) : "") + " ";
            }
            
            return fullCommand;
        }

        public static RequestData GetRequest(string rString, string rEmsUrl, HttpMethod method, KeyValuePair<string, string> rData = new KeyValuePair<string, string>(), RequestData client = null)
        {
            string fullRequestUrl = UrlBuilder(rEmsUrl, rString);
            var patterns = new[] {
                @"login.ws",
                @"loginByProductKey.ws",
                @"activation/target.ws",
                @"productKey/\w{8}-\w{4}-\w{4}-\w{4}-\w{12}.ws",
                @"productKey/\w{8}-\w{4}-\w{4}-\w{4}-\w{12}/activation.ws",
                @"target.ws"
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
                case "login.ws":
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
                        client.httpClientResponseStatus += e.InnerException.InnerException.Message;
                    }
                    catch (HttpRequestException hE)
                    {
                        client.httpClientResponseStatus += hE.Message;
                    }
                    break;

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
                    if (client != null && client.httpClient != null)
                    {
                        if (client.httpClientResponseStatus == "OK")
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
                                client.httpClientResponseStatus += e.InnerException.InnerException.Message + " | in get update by C2V request.";
                            }
                            catch (HttpRequestException hE)
                            {
                                client.httpClientResponseStatus += hE.Message + " | in get update by C2V request.";
                            }
                        }
                    }
                    else
                    {
                        client.httpClientResponseStatus = "Not set HttpClient instance.";
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
