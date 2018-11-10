using SFB;
using System;
using System.IO;
using System.Net;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Net.Sockets;
using System.Collections;
using UnityEngine.Networking;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class Main : MonoBehaviour {

	[SerializeField] private InputField cameraIPField;
	[SerializeField] private Button initialFileButton;
	[SerializeField] private Text downloadDirText;
	[SerializeField] private Text loaderModalStatusText;
	[SerializeField] private GameObject loaderModalGameObject;
	[SerializeField] private GameObject rotatingLoaderGameObject;	
	[SerializeField] private Text informModalMessage;
	[SerializeField] private GameObject informModalGameObject;
	[SerializeField] private Text selectedVideoTypeText;
	[SerializeField] private Text selectedVideoDateText;
	[SerializeField] private Text selectedVideoTimeText;
	[SerializeField] private GameObject selectedVideoModal;
	[SerializeField] private VideoPlayer player;
	private string downloadDir;
	private string fileList;
	private string camIP;
	private string lastSelectedVideoURL;

	void Start() {
		Application.targetFrameRate = 60;
		UpdateDownloadDirectory(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
		SearchForCamera();
	}

	void Update() {
		if (player.isPlaying && rotatingLoaderGameObject.activeSelf) {
			rotatingLoaderGameObject.SetActive(false);
		} else if (player.isPrepared && !player.isPlaying && !rotatingLoaderGameObject.activeSelf) {
			rotatingLoaderGameObject.GetComponent<Image>().color = Color.clear;
			rotatingLoaderGameObject.SetActive(true);
		}
	}

	private void OpenLoadingModal(string status) {
		loaderModalStatusText.text = status;

		if (!loaderModalGameObject.activeSelf) {
			loaderModalGameObject.SetActive(true);
			EventSystem.current.SetSelectedGameObject(null);
		}
	}

	private void CloseLoadingModal() {
		if (loaderModalGameObject.activeSelf) {
			loaderModalGameObject.SetActive(false);
		}

		loaderModalStatusText.text = "";
	}

	private void InformUser(string message) {
		informModalMessage.text = message;

		if (!informModalGameObject.activeSelf) {
			informModalGameObject.SetActive(true);
			CloseLoadingModal();
			EventSystem.current.SetSelectedGameObject(null);
		}
	}

	public void CancelAllLoading() {
		StopAllCoroutines();
	}

	public void LoadFiles() {
		StartCoroutine(GetFileList(cameraIPField.text));
	}

	public void SearchForCamera() {
		string localIP = GetLocalIPAddress();
		string[] octets = localIP.Split('.');
		StartCoroutine(CheckForBlackVue(octets[0] + "." + octets[1] + "." + octets[2], 0, true));
	}

	IEnumerator CheckForBlackVue(string firstThreeOctets, int subnetIndex, bool loop) {
		string url = "http://" + firstThreeOctets + "." + subnetIndex + "/blackvue_vod.cgi";
		
		if (loop) {
			for (int i = 0; i < 255; i++) {
				StartCoroutine(CheckForBlackVue(firstThreeOctets, i, false));
			}
		}

		using (UnityWebRequest www = UnityWebRequest.Get(url)) {
			www.timeout = 5;
            yield return www.SendWebRequest();

   	        if (www.isNetworkError || www.isHttpError) {
				//Didn't find BlackVue at this address...
				OpenLoadingModal("Searching for BlackVue... (" + subnetIndex + " / 255)");
       	    } else {
				if (www.downloadedBytes > 200) {
					string blackVueIP = firstThreeOctets + "." + subnetIndex;
					cameraIPField.text = blackVueIP;
					Debug.LogFormat ("Found BlackVue at {0}\n", blackVueIP);
					InformUser("Found BlackVue at " + blackVueIP);
					CloseLoadingModal();
					StopAllCoroutines();
				} else {
					OpenLoadingModal("Searching for BlackVue... (" + subnetIndex + " / 255)");
				}
			}

			if (subnetIndex >= 253) {
				InformUser("Unable to find a BlackVue device on this subnet. Please try entering the IP in manually.");
				StopAllCoroutines();
				CloseLoadingModal();
			}
		}
	}


	public void SelectDownloadFolder() {
		var paths = StandaloneFileBrowser.OpenFolderPanel("Test", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), false);
		foreach (string path in paths) {
			UpdateDownloadDirectory(path);
		}
	}

	private void UpdateDownloadDirectory(string downloadPath) {
		downloadDir = downloadPath;
		downloadDirText.text = "Download Directory: <i>\"" + downloadDir + "\"</i>";
	}

	public void SelectFile(Text filePathText) {
		rotatingLoaderGameObject.GetComponent<Image>().color = Color.black;
		lastSelectedVideoURL = filePathText.text;
		player.url = "http://" + camIP + "/Record/" + lastSelectedVideoURL;
		player.Play();

		try {
			string[] details = filePathText.text.Split('_');
			string date = details[0].Insert(4, "/").Insert(7, "/");
			string time = details[1].Insert(2, ":").Insert(5, ":");
			string type = details[2].Substring(0, 2);
			DateTime newDateTime = DateTime.Parse(date + " " + time);

			string recType = GetRecordingType(type.Substring(0, 1));
			string camType = GetCamType(type.Substring(1, 1));

			selectedVideoTypeText.text = recType + " - " + camType;
			selectedVideoDateText.text = newDateTime.ToShortDateString();
			selectedVideoTimeText.text = newDateTime.ToShortTimeString();
			
			selectedVideoModal.SetActive(true);
			EventSystem.current.SetSelectedGameObject(null);
		} catch (System.Exception e) {
			Debug.LogError(e);
			InformUser("An error occured: " + e.Message);
		}
	}

	public void Download() {
		StartCoroutine(DownloadVideo(camIP, lastSelectedVideoURL));
	}

	private string GetRecordingType(string notation) {
		switch (notation) {
			case "N": 
			return "<color=green>Normal</color>";
			
			case "E": 
			return "<color=red>Event</color>";
			
			case "P": 
			return "<color=black>Parking</color>";
			
			case "M": 
			return "<color=yellow>Manual</color>";

			default:
			return string.Empty;
		}
	}

	private string GetCamType(string notation) {
		switch (notation) {
			case "F":
			return "<color=blue>Front</color>";
			
			case "R":
			return "<color=white>Rear</color>";

			default:
			return string.Empty;
		}
	}

	IEnumerator DownloadVideo(string baseURL, string fileName) {
		string URL = "http://" + baseURL + "/Record/" + fileName;
		using (UnityWebRequest www = UnityWebRequest.Get(URL)) {
			www.timeout = 1000;
			Debug.Log ("will get: " + URL);
			OpenLoadingModal("Beginning download of \"" + fileName + "\"");
			UnityWebRequestAsyncOperation request = www.SendWebRequest();

            while (!request.isDone) {
				OpenLoadingModal("Downloading... " + (www.downloadProgress * 100).ToString("f1") + "%");
                yield return null;
            }

            if (www.isNetworkError || www.isHttpError) {
                Debug.LogError("An error occured when downloading: " + www.error);
				InformUser("An error occured when downloading the video: " + www.error);
				CloseLoadingModal();		
            } else {
                byte[] results = www.downloadHandler.data;
				if (!File.Exists(downloadDir + "/" + fileName)) {
					File.WriteAllBytes(downloadDir + "/" + fileName, results);
				}
				CloseLoadingModal();		
			}
		}
	}

	IEnumerator GetFileList(string ipAddress) {
		OpenLoadingModal("Getting File List...");
		IPAddress testIP;
		if (!IPAddress.TryParse(ipAddress, out testIP)) {
			InformUser("IP Address is not valid!");
			StopAllCoroutines();
			yield return null;
		}

		using (UnityWebRequest www = UnityWebRequest.Get("http://" + ipAddress + "/blackvue_vod.cgi")) {
			www.timeout = 5;
			yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError) {
                Debug.LogError("An error occured when downloading: " + www.error);
				InformUser("An error occured when retrieving the list of videos: " + www.error);
            } else {
                // Show results as text
				fileList = www.downloadHandler.text;
				fileList = fileList.Substring(fileList.IndexOf('n'));

				string[] lines = fileList.Split(
    				new[] { Environment.NewLine },
    				StringSplitOptions.None
				);

				if (lines.Length == 0) {
					InformUser ("An error occured.");
					yield return null;
				}

				Text buttonText;
				Text recTypeText; 
				Text camTypeText;

				foreach (string line in lines) {
					OpenLoadingModal("Parsing Files...");
	
					string[] lineArr = line.Split(',');
					string filePath = lineArr[0];
					string recType = string.Empty;
					string camType = string.Empty;

					if (filePath.Length > 0) {
						recType = filePath.Substring(filePath.Length-6, 1);
						camType = filePath.Substring(filePath.Length-5, 1);
					}
					if (!initialFileButton.interactable) {
						recTypeText = initialFileButton.transform.GetChild(0).GetComponent<Text>();
						buttonText = initialFileButton.transform.GetChild(1).GetComponent<Text>();
						camTypeText = initialFileButton.transform.GetChild(2).GetComponent<Text>();
						initialFileButton.interactable = true;
						camIP = ipAddress;
					} else {
						GameObject newButton = Instantiate(initialFileButton.gameObject, Vector3.zero, Quaternion.identity, initialFileButton.transform.parent) as GameObject;
						recTypeText = newButton.transform.GetChild(0).GetComponent<Text>();
						buttonText = newButton.transform.GetChild(1).GetComponent<Text>();
						camTypeText = newButton.transform.GetChild(2).GetComponent<Text>();
					}

					filePath = filePath.Replace("n:/Record/", "");
					recType = GetRecordingType(recType);
					camType = GetCamType(camType);

					recTypeText.text = recType;
					buttonText.text = filePath;
					camTypeText.text = camType;
				}

				CloseLoadingModal();
            }
        }
	}

	public static string GetLocalIPAddress() {
		string localIP;
		using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0)) {
	    	socket.Connect("8.8.8.8", 65530);
    		IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
  	  		localIP = endPoint.Address.ToString();
			return localIP;
		}
    }
}
