namespace RoboCare.UGS
{
// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Text;
// using Best.HTTP;
// using Best.HTTP.Response;
// using UnityEngine;
// using System.Linq;
// using Newtonsoft.Json;
// using Cysharp.Threading.Tasks;

// BestHTTP 에셋 import 후 주석 풀고 사용 
// RoboCare.UGS.asmdef Best HTTP 어셈블리를 추가해서 사용

// public sealed class HibomiApiClient
// {
//     public string API_URL_DEV = DefaultEndpoints.GameApiDev;
//     public string API_URL = DefaultEndpoints.GameApi;

//     public string robotAPI_URL = DefaultEndpoints.RobotApi;

//     public static readonly string TEST_ROBOT_ID = "B2V02-00";
//     public static readonly string kSessionId = "HIBOMISESSIONID";
//     public string robot_id = "";

//     private List<string> m_SetCookies;
//     private string sessionId;
//     private string authorizationToken;

//     private bool svpMode = false;
//     // 수정
//     private string _robotType = "bomi1";

//     private static HibomiApiClient _instance;
//     public static HibomiApiClient Instance()
//     {
//         return _instance ??= new HibomiApiClient();
//     }
    
//     public HibomiApiClient()
//     {
//     }

//     /// <summary>
//     /// 토큰 및 쿠기정보 획득 
//     /// /// Cookie 예시)
//     /// HIBOMISESSIONID=DACEB58363D631A6C2AA72A3E4EDB396; X-AUTH-TOKEN=eyJhbGciOiJIUzI1NiJ9.eyJpYXQiOjE3MDMyMDQ0MTgsImV4cCI6MTcwMzQ2MzYxOCwic3ViIjoiQjJWMDItMDAifQ.fGfuwfuE-AAKj4PFwaEC6oQ8Yq8WrGvwF1LYom0YwC8; XSRF-TOKEN=a54893bd-99d1-4842-9164-9b0e8fde1eb0
//     /// </summary>
//     public async UniTask<string> GetTokenAndSetCookie()
//     {
//         string robot = "";
//         string password = "";
//         string version = "";

//         await UniTask.SwitchToMainThread();
//         if (PlayerPrefs.GetString(PrefKeys.SvpMode).Equals("on"))
//             svpMode = true;
//         else if (PlayerPrefs.GetString(PrefKeys.SvpMode).Equals("off"))
//             svpMode = false;

//         int kRobotType = PlayerPrefs.GetInt(PrefKeys.RobotType);
//         _robotType = kRobotType switch
//         {
//             0 => "bomi1",
//             1 => "bomi2",
//             2 => "cami",
//             3 => "lite",
//             _ => _robotType
//         };

//         robot_id = PlayerPrefs.GetString(PrefKeys.RobotId);
//         robot = PlayerPrefs.GetString(PrefKeys.RobotId);

//         API_URL = PlayerPrefs.GetString(PrefKeys.GameApi);
//         string ssoUrl = svpMode ? $"{API_URL}/svp/sso" : $"{API_URL}/v1/sso";
        
//         if (API_URL.Contains("devapi.hibomi") || API_URL.Contains("api.hibomi")) 
//         {
//             LogApi.Log("신 버전 Hibomi API URL");
//             password = ApiKeyProvider.GetKey();
//         }
//         else 
//         {
//             LogApi.Log("구 버전 Hibomi API URL");
//             password = "API_Call_eee77a74-885e-4414-ba2f-1b7e1281186e";
//         }
        
//         version = PlayerPrefs.GetString(PrefKeys.Version);

//         // 필수 값들이 비어있는지 확인
//         if (string.IsNullOrEmpty(robot) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(version))
//         {
//             LogApi.LogError($"[ERROR] 필수 값이 누락됨 - robot: {robot}, password: {password}, version: {version}");
//             return null;
//         }

//         var request = new HTTPRequest(new UriBuilder(ssoUrl)
//         {
//             Query = $"u={Base64Encode(robot)}&p={Base64Encode(password)}&v={Base64Encode(version)}"
//         }.Uri);

//         var resp = await request.GetHTTPResponseAsync();

//         try
//         {
//             var json = JsonConvert.DeserializeObject<Dictionary<string, string>>(resp.DataAsText);

//             if (!json.ContainsKey("accessToken"))
//             {
//                 LogApi.LogError("[API] accessToken 누락됨!");
//                 return null;
//             }

//             authorizationToken = json["accessToken"];
//             sessionId = json.ContainsKey("sessionId") ? json["sessionId"] : "";

//             LogApi.Log($"[API] 토큰획득 >> token:{authorizationToken}");

//             return sessionId;
//         }
//         catch (Exception ex)
//         {
//             LogApi.LogError("[API] 토큰 파싱 실패: " + ex.Message);
//             return null;
//         }
//     }

//     private bool SetAuthorizationHeader(HTTPRequest request)
//     {
//         if (string.IsNullOrEmpty(authorizationToken))
//         {
//             LogApi.LogWarning("[API] Authorization Token 없음");
//             return false;
//         }

//         string authorizationText = $"Bearer {authorizationToken}";
//         request.SetHeader("Authorization", authorizationText);
//         request.SetHeader("Content-Type", "application/json; charset=UTF-8");

//         LogApi.Log($"[API] Authorization Header 설정됨 >> {authorizationText}");
//         return true;
//     }

//     // ReSharper disable Unity.PerformanceAnalysis
//     /// <summary>
//     /// 게임데이터 저장 
//     /// </summary>
//     /// <param name="data"></param>
//     public async UniTask<bool> SendGameScore(List<GameScore> datas)
//     {
//         sessionId ??= await GetTokenAndSetCookie();

//         string url = svpMode ? $"{API_URL}/svp/casualGame" : $"{API_URL}/v1/casualGame";
//         var request = new HTTPRequest(new Uri(url), HTTPMethods.Post);
//         LogApi.Log($"[SendGameScore] >> url: {url}, svpMode: {svpMode}");
        
//         if (!SetAuthorizationHeader(request))
//         {
//             LogApi.Log("[API] 데이터 저장실패: 인증정보 없음");
//             return false;
//         }

//         var requestDataSvp = new GameDataRequest_SVP()
//         {
//             robotId = robot_id,
//             robotType = _robotType,
//             status = "on",
//             data = datas.Select(e => new GameData()
//             {
//                 historyId = e.uuid,
//                 userId = $"{e.userId}",
//                 contentType = "casualGame",
//                 contentId = $"{e.gameId}",
//                 contentData = new ContentData()
//                 {
//                     level = $"{e.level}",
//                     score = $"{e.score}"
//                 },
//                 startTime = $"{GetTime(e.startTime)}",
//                 finishTime = $"{GetTime(e.finishTime)}",
//                 useTime = $"{(GetTime(e.finishTime) - GetTime(e.startTime)) / 1000}",
//             }).ToList()
//         };

//         string serializedData = JsonConvert.SerializeObject(requestDataSvp);
//         LogApi.Log($"{JsonConvert.SerializeObject(requestDataSvp, Newtonsoft.Json.Formatting.Indented)}");

//         request.UploadSettings.UploadStream = new MemoryStream(Encoding.UTF8.GetBytes(serializedData));
//         LogApi.Log($"[API] 데이터 저장 요청 v1 >> {serializedData}");

//         try
//         {
//             var response = await request.GetHTTPResponseAsync();
//             var responseData = JsonConvert.DeserializeObject<GameDataResponse>(response.DataAsText);
//             LogApi.Log($"responseData >> code : {responseData.code}, msg : {responseData.msg}, rst : {responseData.rst}, errmsg : {responseData.errMsg}");

//             // if (responseData.code == "S001")
//             if (responseData.code.StartsWith("S"))
//             {
//                 LogApi.Log($"[API] 데이터 저장 성공(v1) : {response.DataAsText}");
//                 return true;
//             }
//             else
//             {
//                 LogApi.Log($"[API] 저장 실패(v1) : {response.DataAsText}");
//                 return false;
//             }
//         }
//         catch (Exception ex)
//         {
//             LogApi.Log($"[API] HTTP 요청 실패(v1) : {ex.Message}");
//             return false;
//         }
//     }
    
//     // Get robot ID
//     public async UniTask<bool> GetRobotID()
//     {
//         robotAPI_URL = PlayerPrefs.GetString(PrefKeys.RobotApi);
//         var request = new HTTPRequest(new Uri(robotAPI_URL + "robot-id"), HTTPMethods.Get);

//         try
//         {
//             var response = await request.GetHTTPResponseAsync();
//             robot_id = response.DataAsText.Replace("\"", "");

//             PlayerPrefs.SetString(PrefKeys.RobotId, robot_id);
//             PlayerPrefs.Save();

//             _ = GetCurrentUser();

//             return true;
//         }
//         catch (Exception ex)
//         {
//             LogApi.Log($"[API] RobotIdGet 실패 : {ex.Message}");
//             return false;
//         }
//     }

//     // GetCurrentUser
//     public async UniTask GetCurrentUser()
//     {
//         if (PlayerPrefs.GetString(PrefKeys.GetUserData).Equals("off")) return;

//         robotAPI_URL = PlayerPrefs.GetString(PrefKeys.RobotApi);
//         robot_id = PlayerPrefs.GetString(PrefKeys.RobotId);
//         var request = new HTTPRequest(new Uri($"{robotAPI_URL}v3/{robot_id}/users/profiles/current-user-id"), HTTPMethods.Get);

//         try
//         {
//             var response = await request.GetHTTPResponseAsync();
//             string input = response.DataAsText;

//             string currentUserID = input.Replace("\"", "");
            
//             PlayerPrefs.SetString(PrefKeys.User, currentUserID);
//             PlayerPrefs.Save();

//             _ = GetCurrentUserName();
//         }
//         catch(Exception ex)
//         {
//             LogApi.Log($"[API] UserIDGet 실패 : {ex.Message}");
//         }
//     }

//     public async UniTask GetCurrentUserName()
//     {
//         robotAPI_URL = PlayerPrefs.GetString(PrefKeys.RobotApi);
//         string robot_id = PlayerPrefs.GetString(PrefKeys.RobotId);
//         var request = new HTTPRequest(new Uri($"{robotAPI_URL}v3/{robot_id}/users/profiles/current-user-name"), HTTPMethods.Get);
//         LogApi.Log(request);
    
//         try
//         {
//             var response = await request.GetHTTPResponseAsync();
//             string input = response.DataAsText;

//             string currentUserName = input.Replace("\"", "");
            
//             PlayerPrefs.SetString(PrefKeys.UserName, currentUserName);
//             PlayerPrefs.Save();
//         }
//         catch(Exception ex)
//         {
//             LogApi.Log($"[API] User Name Get 실패 : {ex.Message}");
//         }
//     }

//     public static string Base64Encode(string plainText)
//     {
//         var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
//         return Convert.ToBase64String(plainTextBytes);
//     }

//     /// <summary>
//     /// currenTimeMillis gives the number of milliseconds since 1. Jan 1970, so if you need the exact same answer you want something like:
//     /// </summary>
//     public static long GetTime(DateTimeOffset time)
//     {
//         return (long)(time.DateTime - new DateTime(1970,1,1)).TotalMilliseconds;
//     }
// } 
}
