using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.UI;

public class MenuScript : MonoBehaviour {
    public GameObject MainMenuCanvas;
    public GameObject ServerLoadedCanvas;
    public GameObject HostServerCanvas;
    public GameObject JoinServerCanvas;
    public GameObject SettingsCanvas;
	public Toggle JoinServerOnHost;
    public GameObject NewMapCanvas;
	public GameObject LoadMapCanvas;
    public TMP_InputField newMapNameField;
    public TMP_InputField newMapSeedField;
	public TMP_InputField UsernameField;
	public TMP_Dropdown LoadMapDropdown;
	public GameObject MessageSending;
    public Button HostServerButton;
    public Button createMapButton;
    public Button newMapBackButton;

    public void Start4() {
		JoinServerOnHost.isOn = GetComponent<MainScript>().settings.joinServerOnHost;
    }

    public void HostServer() {
		MainMenuCanvas.SetActive(false);
		HostServerCanvas.SetActive(true);
    }

    public void JoinServer() {
		MainMenuCanvas.SetActive(false);
		JoinServerCanvas.SetActive(true);
		GetComponent<MetaNetworkManager>().Client.Send(Riptide.Message.Create(Riptide.MessageSendMode.Reliable, MetaNetworkManager.MessageId.ServerList));
		MessageSending.SetActive(false);
    }

    public void Settings() {
		MainMenuCanvas.SetActive(false);
		SettingsCanvas.SetActive(true);
    }

    public void Quit() {
#if UNITY_EDITOR
		UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void BackToMainMenu() {
		HostServerCanvas.SetActive(false);
		NewMapCanvas.SetActive(false);
		JoinServerCanvas.SetActive(false);
		SettingsCanvas.SetActive(false);
		MainMenuCanvas.SetActive(true);
		MessageSending.SetActive(true);
    }

    public void BackToHostServer() {
		HostServerCanvas.SetActive(true);
		NewMapCanvas.SetActive(false);
		LoadMapCanvas.SetActive(false);
    }

    public void ExistingMap() {
		HostServerCanvas.SetActive(false);
		string[] paths = Directory.GetFiles(Application.persistentDataPath + "/maps");
		LoadMapDropdown.options = new();
		foreach (string path in paths) {
			if (new FileInfo(path).Name.Contains(".paperwars-map")) {
				LoadMapDropdown.options.Add(new TMP_Dropdown.OptionData(Path.GetFileNameWithoutExtension(new FileInfo(path).Name)));
			}
		}
		LoadMapCanvas.SetActive(true);
    }

    public void NewMap() {
		HostServerCanvas.SetActive(false);
		NewMapCanvas.SetActive(true);
    }

    public void NewMapConfirm() {
		newMapNameField.interactable = false;
		newMapSeedField.interactable = false;
		createMapButton.interactable = false;
		newMapBackButton.interactable = false;
		MainScript.PrintMessage("Generating World...\n(Remember to port forward the port 35725 in your router settings!)");
		if (newMapNameField.text == "") {newMapNameField.text = "New Map";}
		if (newMapSeedField.text == "") {newMapSeedField.text = new System.Random().Next().ToString();}
		GetComponent<MainScript>().serverMap = GetComponent<MainScript>().GenerateMap(newMapNameField.text, int.Parse(newMapSeedField.text));
		MainScript.PrintMessage("Starting Server...\n(Remember to port forward the port 35725 in your router settings!)");
		GetComponent<NetworkManager>().StartHost();
	}

    public void JoinServerConfirm(string serverName, string serverHostName, MainScript.Version serverVersion, string ip) {
		JoinServerCanvas.SetActive(false);
		GetComponent<NetworkManager>().connectedServerName = serverName;
		GetComponent<NetworkManager>().connectedServerHostName = serverHostName;
		GetComponent<NetworkManager>().connectedServerVersion = serverVersion.ToString();
		GetComponent<NetworkManager>().JoinGame(ip);
    }

	public void LoadMapConfirm() {
		LoadMapCanvas.SetActive(false);
		LoadMapFromFile(Application.persistentDataPath + "/maps/" + LoadMapDropdown.options[LoadMapDropdown.value].text + ".paperwars-map");
    }

    public void LoadMapFromFile(string path) {
		GetComponent<MainScript>().serverMap = JsonUtility.FromJson<MainScript.Map>(Base64.Decode(File.ReadAllText(path)));
		GetComponent<NetworkManager>().StartHost();
		if (!GetComponent<MainScript>().settings.joinServerOnHost) {
			BackToMainMenu();
			NewMapCanvas.SetActive(false);
		}
    }
}
