﻿// // ----------------------------------------------------------------------
// // <copyright file="TranslationServiceFacade.cs" company="Microsoft Corporation">
// // Copyright (c) Microsoft Corporation.
// // All rights reserved.
// // THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY
// // KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
// // IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
// // PARTICULAR PURPOSE.
// // </copyright>
// // ----------------------------------------------------------------------
// // <summary>TranslationServiceFacade.cs</summary>
// // ----------------------------------------------------------------------

namespace TranslationAssistant.TranslationServices.Core
{
    #region Usings

    using Microsoft.Translator.API;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.ServiceModel;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using TranslatorService;

    #endregion

    public class TranslationServiceFacade
    {
        private const int MillisecondsTimeout = 500;
        #region Static Fields

        public static Dictionary<string, string> AvailableLanguages = new Dictionary<string, string>();

        public static int maxrequestsize = 5000;
        public static int maxelements = 25;

        private static string _CategoryID;
        public static string CategoryID{
            get { return _CategoryID; }
            set { _CategoryID = value; }
        }

        /// <summary>
        /// Used only for on-prem installs
        /// </summary>
        private static string _AppId;
        public static string AppId
        {
            get { return _AppId; }
            set { _AppId = value; }
        }

        /// <summary>
        /// Used only for on-prem installs
        /// </summary>
        private static string _Adv_CategoryId;
        public static string Adv_CategoryId
        {
            get { return _Adv_CategoryId; }
            set { _Adv_CategoryId = value; }
        }

        /// <summary>
        /// Used only for on-prem installs
        /// </summary>
        private static bool _UseAdvancedSettings;
        public static bool UseAdvancedSettings
        {
            get { return _UseAdvancedSettings; }
            set { _UseAdvancedSettings = value; }
        }


        private static string _AzureKey;
        public static string AzureKey
        {
            get { return _AzureKey; }
            set { _AzureKey = value; }
        }

        private static string _TmxFileName = "_DocumentTranslator.TMX";
        /// <summary>
        /// Allows to set the file name to save the TMX under.
        /// No effect if CreateTMXOnTranslate is not set.
        /// </summary>
        public static string TmxFileName
        {
            get { return _TmxFileName; }
            set { _TmxFileName = value; }
        }
        /// <summary>
        /// Create a TMX file containing source and target while translating. 
        /// </summary>
        public static bool CreateTMXOnTranslate { get; set; } = false;

        /// <summary>
        /// End point address for V2 of the Translator API
        /// </summary>
        public static string EndPointAddress { get; set; } = "https://api.microsofttranslator.com";

        /// <summary>
        /// End point address for V3 of the Translator API
        /// </summary>
        public static string EndPointAddressV3 { get; set; } = "https://api.cognitive.microsofttranslator.com";
        
        /// <summary>
        /// Hold the version of the API to use. Default to V3, fall back to V2 if category is set and is not available in V3. 
        /// </summary>
        public enum UseVersion { V2, V3, unknown };
        public static UseVersion useversion = UseVersion.V3;

        private enum AuthMode { Azure, AppId };
        private static AuthMode authMode = AuthMode.Azure;
        private static string appid = null;


        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// Check if the Translation service is ready to use, with a valid client ID and secret
        /// </summary>
        /// <returns>true if ready, false if not</returns>
        public static bool IsTranslationServiceReady()
        {
            switch (authMode)
            {
                case AuthMode.Azure:
                    try
                    {
                        AzureAuthToken authTokenSource = new AzureAuthToken(_AzureKey);
                        string headerValue = authTokenSource.GetAccessToken();
                        var bind = new BasicHttpBinding { Name = "BasicHttpBinding_LanguageService" };
                        var epa = new EndpointAddress(EndPointAddress.Replace("https:", "http:") + "/V2/soap.svc");
                        LanguageServiceClient client = new LanguageServiceClient(bind, epa);
                        string[] languages = new string[1];
                        languages[0] = "en";
                        client.GetLanguageNames(GetHeaderValue(), Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName, languages, false);
                    }
                    catch { return false; }
                    break;
                case AuthMode.AppId:
                    try
                    {
                        var bind = new BasicHttpBinding { Name = "BasicHttpBinding_LanguageService" };
                        var epa = new EndpointAddress(EndPointAddress.Replace("https:", "http:") + "/V2/soap.svc");
                        LanguageServiceClient client = new LanguageServiceClient(bind, epa);
                        string[] languages = new string[1];
                        languages[0] = "en";
                        client.GetLanguageNames(GetHeaderValue(), Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName, languages, false);
                    }
                    catch { return false; }
                    break;
                default:
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Test if a given category value is a valid category in the system.
        /// Works across V2 and V3 of the API.
        /// </summary>
        /// <param name="category">Category ID</param>
        /// <returns>True if the category is valid</returns>
        public static bool IsCategoryValid(string category)
        {
            useversion = UseVersion.V3;
            if (category == "") return true;
            if (category == string.Empty) return true;
            if (category.ToLower() == "general") return true;
            if (category.ToLower() == "generalnn") return true;

            //Test V2 API and V3 API both

            bool testV2 = IsCategoryValidV2(category);
            if (testV2) return true;
            else
            {
                Task<bool> testV3 = IsCategoryValidV3Async(category);
                return testV3.Result;
            }
        }

        private static async Task<bool> IsCategoryValidV3Async(string category)
        {
            bool returnvalue = true;

            for (int i = 0; i < 3; i++)
            {
                try
                {
                    string[] teststring = { "Test" };
                    Task<string[]> translateTask = TranslateV3Async(teststring, "en", "he", category, "text/plain");
                    await translateTask.ConfigureAwait(false);
                    if (translateTask.Result == null) return false; else return true;
                }
                catch (Exception e)
                {
                    string error = e.Message;
                    returnvalue = false;
                    Thread.Sleep(1000);
                    continue;
                }
            }
            return returnvalue;
        }


        private static bool IsCategoryValidV2(string category)
        {
            //Test V2 API
            //It may take a while until the category is loaded on server
            bool returnvalue = true;
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    string headerValue = GetHeaderValue();
                    var bind = new BasicHttpBinding { Name = "BasicHttpBinding_LanguageService" };
                    var epa = new EndpointAddress(EndPointAddress.Replace("https", "http") + "/V2/soap.svc");
                    LanguageServiceClient client = new LanguageServiceClient(bind, epa);
                    client.Translate(headerValue, "Test", "en", "fr", "text/plain", category, string.Empty);
                    returnvalue = true;
                    break;
                }
                catch (Exception e)
                {
                    string error = e.Message;
                    returnvalue = false;
                    Thread.Sleep(1000);
                    continue;
                }
            }
            return returnvalue;
        }


        /// <summary>
        /// Call once to initialize the static variables
        /// </summary>
        public static void Initialize()
        {
            LoadCredentials();

            //Inspect the given Azure Key to see if this is host with appid auth
            string[] AuthComponents = _AzureKey.Split('?');
            if (AuthComponents.Length > 1)
            {
                EndPointAddress = AuthComponents[0];
                string[] appidComponents = AuthComponents[1].ToLowerInvariant().Split('=');
                if (appidComponents[0] == "appid")
                {
                    authMode = AuthMode.AppId;
                    appid = appidComponents[1];
                }
                else return;
            }
            try { if (!IsTranslationServiceReady()) return; }
            catch { return; }
            var bind = new BasicHttpBinding { Name = "BasicHttpBinding_LanguageService" };
            var epa = new EndpointAddress(EndPointAddress.Replace("https:", "http:") + "/V2/soap.svc");   //for some reason GetLanguagesForTranslate doesn't work with SSL.
            LanguageServiceClient client = new LanguageServiceClient(bind, epa);
            string headerValue = GetHeaderValue();
            string[] languages = client.GetLanguagesForTranslate(headerValue);
            string[] languagenames = client.GetLanguageNames(headerValue, Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName, languages, false);
            for (int i = 0; i < languages.Length; i++)
            {
                if (!AvailableLanguages.ContainsKey(languages[i]))
                {
                    AvailableLanguages.Add(languages[i], languagenames[i]);
                }
            }
            client.Close();
        }

        private static string GetHeaderValue()
        {
            string headerValue = null;
            if (authMode == AuthMode.Azure)
            {
                AzureAuthToken authTokenSource = new AzureAuthToken(_AzureKey);
                headerValue = authTokenSource.GetAccessToken();
            }
            else headerValue = appid;
            return headerValue;
        }

        /// <summary>
        /// Loads credentials from settings file.
        /// Doesn't need to be public, because it is called during Initialize();
        /// </summary>
        private static void LoadCredentials()
        {
            _AzureKey = Properties.Settings.Default.AzureKey;
            _CategoryID = Properties.Settings.Default.CategoryID;
            _AppId = Properties.Settings.Default.AppId;
            EndPointAddress = Properties.Settings.Default.EndPointAddress;
            _UseAdvancedSettings = Properties.Settings.Default.UseAdvancedSettings;
            _Adv_CategoryId = Properties.Settings.Default.Adv_CategoryID;
        }

        /// <summary>
        /// Saves credentials Azure Key and categoryID to the personalized settings file.
        /// </summary>
        public static void SaveCredentials()
        {
            Properties.Settings.Default.AzureKey = _AzureKey;
            Properties.Settings.Default.CategoryID = _CategoryID;
            Properties.Settings.Default.AppId = _AppId;
            Properties.Settings.Default.EndPointAddress = EndPointAddress;
            Properties.Settings.Default.UseAdvancedSettings = _UseAdvancedSettings;
            Properties.Settings.Default.Adv_CategoryID = _Adv_CategoryId;
            Properties.Settings.Default.Save();
        }

        /// <summary>
        /// Takes a single language name and returns the matching language code. OK to pass a language code.
        /// </summary>
        /// <param name="languagename"></param>
        /// <returns></returns>
        public static string LanguageNameToLanguageCode(string languagename)
        {
            if (AvailableLanguages.ContainsKey(languagename))
            {
                return languagename;
            }
            else if (AvailableLanguages.ContainsValue(languagename))
            {
                return AvailableLanguages.First(t => t.Value == languagename).Key;
            }
            else
            {
                throw new ArgumentException(String.Format("LanguageNameToLanguageCode: Language name {0} not found.", languagename));
            }
        }

        public static string LanguageCodeToLanguageName(string languagecode)
        {
            if (AvailableLanguages.ContainsValue(languagecode))
            {
                return languagecode;
            }
            else if (AvailableLanguages.ContainsKey(languagecode))
            {
                return AvailableLanguages.First(t => t.Key == languagecode).Value;
            }
            else
            {
                throw new ArgumentException(String.Format("LanguageCodeToLanguageName: Language code {0} not found.", languagecode));
            }
        }


        /// <summary>
        /// Retrieve word alignments during translation
        /// </summary>
        /// <param name="texts">Array of text strings to translate</param>
        /// <param name="from">From language</param>
        /// <param name="to">To language</param>
        /// <param name="alignments">Call by reference: array of alignment strings in the form [[SourceTextStartIndex]:[SourceTextEndIndex]–[TgtTextStartIndex]:[TgtTextEndIndex]]</param>
        /// <returns>Translated array elements</returns>
        public static string[] GetAlignments(string[] texts, string from, string to, ref string[] alignments)
        {
            string fromCode = string.Empty;
            string toCode = string.Empty;

            if (from.ToLower() == "Auto-Detect".ToLower() || from == string.Empty)
            {
                fromCode = string.Empty;
            }
            else
            {
                try { fromCode = AvailableLanguages.First(t => t.Value == from).Key; }
                catch { fromCode = from; }
            }

            toCode = LanguageNameToLanguageCode(to);

            string headerValue = GetHeaderValue();
            var bind = new BasicHttpBinding
            {
                Name = "BasicHttpBinding_LanguageService",
                OpenTimeout = TimeSpan.FromMinutes(5),
                CloseTimeout = TimeSpan.FromMinutes(5),
                ReceiveTimeout = TimeSpan.FromMinutes(5),
                MaxReceivedMessageSize = int.MaxValue,
                MaxBufferPoolSize = int.MaxValue,
                MaxBufferSize = int.MaxValue,
                Security =
                                   new BasicHttpSecurity { Mode = BasicHttpSecurityMode.Transport }
            };

            var epa = new EndpointAddress(EndPointAddress + "/V2/soap.svc");
            LanguageServiceClient client = new LanguageServiceClient(bind, epa);

            if (String.IsNullOrEmpty(toCode))
            {
                toCode = "en";
            }

            TranslateOptions options = new TranslateOptions();
            options.Category = _CategoryID;

            try
            {
                var translatedTexts2 = client.TranslateArray2(
                    headerValue,
                    texts,
                    fromCode,
                    toCode,
                    options);
                string[] res = translatedTexts2.Select(t => t.TranslatedText).ToArray();
                alignments = translatedTexts2.Select(t => t.Alignment).ToArray();
                return res;
            }
            catch   //try again forcing English as source language
            {
                var translatedTexts2 = client.TranslateArray2(
                    headerValue,
                    texts,
                    "en",
                    toCode,
                    options);
                string[] res = translatedTexts2.Select(t => t.TranslatedText).ToArray();
                alignments = translatedTexts2.Select(t => t.Alignment).ToArray();
                return res;
            }
        }

        /// <summary>
        /// Translates a string
        /// </summary>
        /// <param name="text">String to translate</param>
        /// <param name="from">From language</param>
        /// <param name="to">To language</param>
        /// <param name="contentType">Content Type</param>
        /// <returns></returns>
        public static string TranslateString(string text, string from, string to, string contentType)
        {
            string[] texts = new string[1];
            texts[0] = text;
            string[] results = TranslateArray(texts, from, to, contentType);
            return results[0];
        }


        /// <summary>
        /// Translates an array of strings from the from langauge code to the to language code.
        /// From langauge code can stay empty, in that case the source language is auto-detected, across all elements of the array together.
        /// </summary>
        /// <param name="texts">Array of strings to translate</param>
        /// <param name="from">From language code. May be empty</param>
        /// <param name="to">To language code. Must be a valid language</param>
        /// <returns></returns>
        public static string[] TranslateArray(string[] texts, string from, string to)
        {
            return TranslateArray(texts, from, to, "text/plain");
        }


        public static string[] TranslateArray(string[] texts, string from, string to, string contentType)
        {
            string fromCode = string.Empty;
            string toCode = string.Empty;

            if (from.ToLower() == "Auto-Detect".ToLower() || from == string.Empty)
            {
                fromCode = string.Empty;
            }
            else
            {
                try { fromCode = AvailableLanguages.First(t => t.Value == from).Key; }
                catch { fromCode = from; }
            }

            toCode = LanguageNameToLanguageCode(to);


            if (useversion == UseVersion.V2)
            {
                return TranslateArrayV2(texts, fromCode, toCode, contentType);
            }
            else
            {
                Task<string[]> TranslateTask = TranslateV3Async(texts, fromCode, toCode, _CategoryID, contentType);
                if ((TranslateTask.Result == null) && IsCustomCategory(_CategoryID))
                {
                    useversion = UseVersion.V2;
                    return TranslateArrayV2(texts, fromCode, toCode, contentType);
                }
                return TranslateTask.Result;
            }

        }

        private static bool IsCustomCategory(string categoryID)
        {
            string category = categoryID.ToLower();
            if (category == "general") return false;
            if (category == null) return false;
            if (category == "generalnn") return false;
            if (category == string.Empty) return false;
            return true;
        }


        /// <summary>
        /// Translates an array of strings from the from langauge code to the to language code.
        /// From langauge code can stay empty, in that case the source language is auto-detected, across all elements of the array together.
        /// </summary>
        /// <param name="texts">Array of strings to translate</param>
        /// <param name="from">From language code. May be empty</param>
        /// <param name="to">To language code. Must be a valid language</param>
        /// <param name="contentType">Whether this is plain text or HTML</param>
        /// <returns></returns>
        private static string[] TranslateArrayV2(string[] texts, string from, string to, string contentType)
        {
            string headerValue = GetHeaderValue();
            var bind = new BasicHttpBinding
                           {
                               Name = "BasicHttpBinding_LanguageService",
                               OpenTimeout = TimeSpan.FromMinutes(5),
                               CloseTimeout = TimeSpan.FromMinutes(5),
                               ReceiveTimeout = TimeSpan.FromMinutes(5),
                               MaxReceivedMessageSize = int.MaxValue,
                               MaxBufferPoolSize = int.MaxValue,
                               MaxBufferSize = int.MaxValue,
                               Security =
                                   new BasicHttpSecurity { Mode = BasicHttpSecurityMode.Transport }
                           };

            var epa = new EndpointAddress(EndPointAddress + "/V2/soap.svc");
            LanguageServiceClient client = new LanguageServiceClient(bind, epa);

            if (String.IsNullOrEmpty(to))
            {
                to = "en";
            }

            TranslateOptions options = new TranslateOptions();
            options.Category = _CategoryID;
            options.ContentType = contentType;
            
            try
            {
                var translatedTexts = client.TranslateArray(
                    headerValue,
                    texts,
                    from,
                    to,
                    options);
                string[] res = translatedTexts.Select(t => t.TranslatedText).ToArray();
                if (CreateTMXOnTranslate) WriteToTmx(texts, res, from, to, options.Category);
                return res;
            }
            catch   //try again forcing English as source language
            {
                var translatedTexts = client.TranslateArray(
                    headerValue,
                    texts,
                    "en",
                    to,
                    options);
                string[] res = translatedTexts.Select(t => t.TranslatedText).ToArray();
                if (CreateTMXOnTranslate) WriteToTmx(texts, res, from, to, options.Category);
                return res;
            }
        }

        private static async Task<string[]> TranslateV3Async(string[] texts, string from, string to, string category, string contentType)
        {
            int retrycount = 0;
            string path = "/translate?api-version=3.0";
            string params_ = "&from=" + from + "&to=" + to;
            string thiscategory = category;
            if (String.IsNullOrEmpty(category))
            {
                thiscategory = null;
            }
            else
            {
                if (thiscategory == "generalnn") thiscategory = null;
                if (thiscategory == "general") thiscategory = null;
            }
            if (thiscategory != null) params_ += "&category=" + System.Web.HttpUtility.UrlEncode(category);
            string uri = EndPointAddressV3 + path + params_;

            ArrayList requestAL = new ArrayList();
            foreach (string text in texts)
            {
                requestAL.Add(new { Text = text } );
            }
            string requestJson = JsonConvert.SerializeObject(requestAL);

            IList<string> resultList = new List<string>();
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                client.Timeout = TimeSpan.FromSeconds(10);
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri(uri);
                request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                request.Headers.Add("Ocp-Apim-Subscription-Key", AzureKey);
                var response = await client.SendAsync(request).ConfigureAwait(false);
                var responseBody = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    switch ((int)response.StatusCode)
                    {
                        case 429:
                        case 500:
                        case 503:
                            System.Diagnostics.Debug.WriteLine("Retry #" + retrycount + " Response: " + (int)response.StatusCode);
                            Thread.Sleep(MillisecondsTimeout);
                            if (retrycount++ > 20) break;
                            else await TranslateV3Async(texts, from, to, category, null);
                            break;
                        default:
                            var errorstring = "ERROR " + response.StatusCode + "\n" + JsonConvert.SerializeObject(JsonConvert.DeserializeObject(responseBody), Formatting.Indented);
                            Exception ex = new Exception(errorstring);
                            throw ex;
                    }
                }
                JArray jaresult = JArray.Parse(responseBody);
                foreach (JObject result in jaresult)
                {
                    string txt = (string)result.SelectToken("translations[0].text");
                    resultList.Add(txt);
                }
            }
            return resultList.ToArray();
        }


        private static void WriteToTmx(string[] texts, string[] res, string from, string to, string comment)
        {
            TranslationMemory TM = new TranslationMemory();
            TranslationUnit TU = new TranslationUnit();
            TM.sourceLangID = from;
            TM.targetLangID = to;
            for (int i=0; i<texts.Length; i++)
            {
                TU.strSource = texts[i];
                TU.strTarget = res[i];
                TU.user = "DocumentTranslator";
                TU.status = TUStatus.good;
                TU.comment = comment;
                TM.Add(TU);
            }
            TM.WriteToTmx(_TmxFileName);
            return;
        }

        /// <summary>
        /// Breaks a piece of text into sentences and returns an array containing the lengths in each sentence. 
        /// </summary>
        /// <param name="text">The text to analyze and break.</param>
        /// <param name="languageID">The language identifier to use.</param>
        /// <returns>An array of integers representing the lengths of the sentences. The length of the array is the number of sentences, and the values are the length of each sentence.</returns>
        public static int[] BreakSentences(string text, string languageID)
        {
            var bind = new BasicHttpBinding
                           {
                               Name = "BasicHttpBinding_LanguageService",
                               OpenTimeout = TimeSpan.FromMinutes(5),
                               CloseTimeout = TimeSpan.FromMinutes(5),
                               ReceiveTimeout = TimeSpan.FromMinutes(5),
                               MaxReceivedMessageSize = int.MaxValue,
                               MaxBufferPoolSize = int.MaxValue,
                               MaxBufferSize = int.MaxValue,
                               Security =
                                   new BasicHttpSecurity { Mode = BasicHttpSecurityMode.Transport }
                           };

            var epa = new EndpointAddress(EndPointAddress + "/V2/soap.svc");
            TranslatorService.LanguageServiceClient client = new LanguageServiceClient(bind, epa);
            string headerValue = GetHeaderValue();
            return client.BreakSentences(headerValue, text, languageID);
        }

        /// <summary>
        /// Breaks a piece of text into sentences and returns an array containing the lengths in each sentence. 
        /// </summary>
        /// <param name="text">The text to analyze and break.</param>
        /// <param name="languageID">The language identifier to use.</param>
        /// <returns>An array of integers representing the lengths of the sentences. The length of the array is the number of sentences, and the values are the length of each sentence.</returns>
        async public static Task<int[]> BreakSentencesAsync(string text, string languageID)
        {
            var bind = new BasicHttpBinding
            {
                Name = "BasicHttpBinding_LanguageService",
                OpenTimeout = TimeSpan.FromMinutes(5),
                CloseTimeout = TimeSpan.FromMinutes(5),
                ReceiveTimeout = TimeSpan.FromMinutes(5),
                MaxReceivedMessageSize = int.MaxValue,
                MaxBufferPoolSize = int.MaxValue,
                MaxBufferSize = int.MaxValue,
                Security =
                    new BasicHttpSecurity { Mode = BasicHttpSecurityMode.Transport }
            };

            var epa = new EndpointAddress(EndPointAddress + "/V2/soap.svc");
            TranslatorService.LanguageServiceClient client = new LanguageServiceClient(bind, epa);
            string headerValue = GetHeaderValue();
            int[] result = { 0 };
            try
            {
                Task<int[]> BSTask = client.BreakSentencesAsync(headerValue, text, languageID);
                result = await BSTask;
            }
            catch
            {
                for (int i = 1; i <= 10; i++)
                {
                    Thread.Sleep(5000 * i);
                    Task<int[]> BSTask = client.BreakSentencesAsync(headerValue, text, languageID);
                    result = await BSTask;
                }
            }
            return result;
        }



        /// <summary>
        /// Adds a translation to Microsoft Translator's translation memory.
        /// </summary>
        /// <param name="originalText">Required. A string containing the text to translate from. The string has a maximum length of 1000 characters.</param>
        /// <param name="translatedText">Required. A string containing translated text in the target language. The string has a maximum length of 2000 characters. </param>
        /// <param name="from">Required. A string containing the language code of the source language. Must be a valid culture name. </param>
        /// <param name="to">Required. A string containing the language code of the target language. Must be a valid culture name. </param>
        /// <param name="rating">Optional. An int representing the quality rating for this string. Value between -10 and 10. Defaults to 1. </param>
        /// <param name="user">Required. A string used to track the originator of the submission. </param>
        public static void AddTranslation(string originalText, string translatedText, string from, string to, int rating, string user)
        {
            var bind = new BasicHttpBinding
            {
                Name = "BasicHttpBinding_LanguageService",
                OpenTimeout = TimeSpan.FromMinutes(5),
                CloseTimeout = TimeSpan.FromMinutes(5),
                ReceiveTimeout = TimeSpan.FromMinutes(5),
                MaxReceivedMessageSize = int.MaxValue,
                MaxBufferPoolSize = int.MaxValue,
                MaxBufferSize = int.MaxValue,
                Security =
                    new BasicHttpSecurity { Mode = BasicHttpSecurityMode.Transport }
            };

            var epa = new EndpointAddress(EndPointAddress + "/V2/soap.svc");
            TranslatorService.LanguageServiceClient client = new LanguageServiceClient(bind, epa);
            string headerValue = GetHeaderValue();
            try
            {
                client.AddTranslation(headerValue, originalText, translatedText, from, to, rating, "text/plain", _CategoryID, user, string.Empty);
            }
            catch
            {
                for (int i = 0; i < 3; i++)
                {
                    Thread.Sleep(MillisecondsTimeout);
                    client.AddTranslation(headerValue, originalText, translatedText, from, to, rating, "text/plain", _CategoryID, user, string.Empty);
                }
            }
            return;
        }

        public struct UserTranslation
        {
            public DateTime CreatedDateUtc;
            public string From;
            public string OriginalText;
            public int Rating;
            public string To;
            public string TranslatedText;
            public string Uri;
            public string User;
        }

        public static UserTranslation[] GetUserTranslations(string fromlanguage, string tolanguage, int skip, int count)
        {
            var bind = new BasicHttpBinding
            {
                Name = "BasicHttpBinding_LanguageService",
                OpenTimeout = TimeSpan.FromMinutes(5),
                CloseTimeout = TimeSpan.FromMinutes(5),
                ReceiveTimeout = TimeSpan.FromMinutes(5),
                MaxReceivedMessageSize = int.MaxValue,
                MaxBufferPoolSize = int.MaxValue,
                MaxBufferSize = int.MaxValue,
                Security =
                    new BasicHttpSecurity { Mode = BasicHttpSecurityMode.Transport }
            };

            var epa = new EndpointAddress(EndPointAddress + "/v2/beta/ctfreporting.svc");
            CtfReportingService.CtfReportingServiceClient client = new CtfReportingService.CtfReportingServiceClient(bind, epa);
            string headerValue = GetHeaderValue();
            CtfReportingService.UserTranslation[] usertranslations = new CtfReportingService.UserTranslation[count];

            usertranslations = client.GetUserTranslations(headerValue, string.Empty, fromlanguage, tolanguage, 0, 10, string.Empty, string.Empty, DateTime.MinValue, DateTime.MaxValue, skip, count, true);

            UserTranslation[] usertranslationsreturn = new UserTranslation[count];
            usertranslationsreturn.Initialize();

            for (int i = 0; i < usertranslations.Length; i++)
            {
                usertranslationsreturn[i].CreatedDateUtc = usertranslations[i].CreatedDateUtc;
                usertranslationsreturn[i].From = usertranslations[i].From;
                usertranslationsreturn[i].OriginalText = usertranslations[i].OriginalText;
                usertranslationsreturn[i].To = usertranslations[i].To;
                usertranslationsreturn[i].TranslatedText = usertranslations[i].TranslatedText;
                usertranslationsreturn[i].Rating = usertranslations[i].Rating;
                usertranslationsreturn[i].Uri = usertranslations[i].Uri;
                usertranslationsreturn[i].User = usertranslations[i].User;
            }

            return usertranslationsreturn;
        }

        #endregion
    }
}