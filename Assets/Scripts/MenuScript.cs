using Riptide;
using Riptide.Utils;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.UI;

public class MenuScript : MonoBehaviour {
    public GameObject MainMenuCanvas;
    public GameObject HostServerCanvas;
    public GameObject JoinServerCanvas;
    public GameObject SettingsCanvas;
	public GameObject AdminPanel;
	public GameObject AccountAdminPanel;
	public GameObject OnlineAccountAdminPanel;
	public TMP_Text OnlineAccountAdminPanelUUID;
	public TMP_InputField OnlineAccountAdminPanelEmailField;
	public TMP_InputField OnlineAccountAdminPanelUsernameField;
	public TMP_InputField OnlineAccountAdminPanelPasswordField;
	public Toggle OnlineAccountAdminPanelMailingList;
	public Toggle OnlineAccountAdminPanelBlacklisted;
	public Toggle OnlineAccountAdminPanelServer;
	public Button OnlineAccountAdminPanelDisconnect;
	public GameObject OfflineAccountAdminPanel;
	public TMP_Text OfflineAccountAdminPanelUUID;
	public TMP_InputField OfflineAccountAdminPanelEmailField;
	public TMP_InputField OfflineAccountAdminPanelUsernameField;
	public TMP_InputField OfflineAccountAdminPanelPasswordField;
	public Toggle OfflineAccountAdminPanelMailingList;
	public Toggle OfflineAccountAdminPanelBlacklisted;
	public Toggle OfflineAccountAdminPanelServer;
	public Toggle JoinServerOnHost;
    public GameObject NewMapCanvas;
	public GameObject LoadMapCanvas;
    public TMP_InputField newMapNameField;
    public TMP_InputField newMapSeedField;
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
		GetComponent<NetworkManager>().Server.Stop();
        GetComponent<NetworkManager>().Client.Disconnect();
		GetComponent<MetaNetworkManager>().Server.Stop();
        GetComponent<MetaNetworkManager>().Client.Disconnect();
		File.Delete(Application.persistentDataPath + "/temp" + GetComponent<NetworkManager>().port);
#if UNITY_EDITOR
		UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

	public void OpenAdminPanel() {
		AdminPanel.SetActive(true);
		MainMenuCanvas.SetActive(false);
		MessageSending.SetActive(false);
		AccountAdminPanel.SetActive(false);
	}

	public void OpenAccountAdminPanel() {
		AdminPanel.SetActive(false);
		foreach (Transform child in GetComponent<MetaNetworkManager>().OnlineAccountScrollViewContent.transform) {
     		GameObject.Destroy(child.gameObject);
 	    }
		 foreach (Transform child in GetComponent<MetaNetworkManager>().OfflineAccountScrollViewContent.transform) {
     		GameObject.Destroy(child.gameObject);
 	    }
	    foreach (var data in GetComponent<MetaNetworkManager>().connectedClients) {
			GameObject button = GameObject.Instantiate(GetComponent<MetaNetworkManager>().ButtonPrefab, GetComponent<MetaNetworkManager>().OnlineAccountScrollViewContent.GetComponent<Transform>());
			button.GetComponentsInChildren<TMP_Text>()[0].text = data.Value.account.username + " (" + data.Value.account.uuid + ")";
			button.GetComponent<Button>().onClick.AddListener(() => {OpenOnlineAccountPanel(data.Key);});
			((RectTransform)button.transform).sizeDelta = new Vector2(400f, 30f);
	    }
		for (int i = 0; i < GetComponent<MetaNetworkManager>().unassignedAccounts.Count; i++) {
			var data = GetComponent<MetaNetworkManager>().unassignedAccounts[i];
			GameObject button = GameObject.Instantiate(GetComponent<MetaNetworkManager>().ButtonPrefab, GetComponent<MetaNetworkManager>().OfflineAccountScrollViewContent.GetComponent<Transform>());
			button.GetComponentsInChildren<TMP_Text>()[0].text = data.username + " (" + data.uuid + ")";
			button.GetComponent<Button>().onClick.AddListener(() => {OpenOfflineAccountPanel(i-1);});
			((RectTransform)button.transform).sizeDelta = new Vector2(400f, 30f);
	    }
		AccountAdminPanel.SetActive(true);
		OnlineAccountAdminPanel.SetActive(false);
		OfflineAccountAdminPanel.SetActive(false);
	}

	public void OpenOnlineAccountPanel(ushort id) {
		AccountAdminPanel.SetActive(false);
		OnlineAccountAdminPanel.SetActive(true);
		OfflineAccountAdminPanel.SetActive(false);
		MetaNetworkManager.Account account = GetComponent<MetaNetworkManager>().connectedClients[id].account;
		OnlineAccountAdminPanelUUID.text = account.uuid;
		OnlineAccountAdminPanelEmailField.text = account.email;
		OnlineAccountAdminPanelUsernameField.text = account.username;
		OnlineAccountAdminPanelPasswordField.text = account.password;
		OnlineAccountAdminPanelMailingList.isOn = account.mailingList;
		OnlineAccountAdminPanelBlacklisted.isOn = account.blacklisted;
		OnlineAccountAdminPanelServer.isOn = account.server;
		OnlineAccountAdminPanelDisconnect.onClick.RemoveAllListeners();
		OnlineAccountAdminPanelDisconnect.onClick.AddListener(() => {GetComponent<MetaNetworkManager>().Server.DisconnectClient(id, Message.Create().AddByte(1));});
	}

	public void OpenOfflineAccountPanel(int id) {
		AccountAdminPanel.SetActive(false);
		OnlineAccountAdminPanel.SetActive(false);
		OfflineAccountAdminPanel.SetActive(true);
		MetaNetworkManager.Account account = GetComponent<MetaNetworkManager>().unassignedAccounts[id];
		OfflineAccountAdminPanelUUID.text = account.uuid;
		OfflineAccountAdminPanelEmailField.text = account.email;
		OfflineAccountAdminPanelUsernameField.text = account.username;
		OfflineAccountAdminPanelPasswordField.text = account.password;
		OfflineAccountAdminPanelMailingList.isOn = account.mailingList;
		OfflineAccountAdminPanelBlacklisted.isOn = account.blacklisted;
		OfflineAccountAdminPanelServer.isOn = account.server;
		OfflineAccountAdminPanelEmailField.onSubmit.RemoveAllListeners();
		OfflineAccountAdminPanelUsernameField.onSubmit.RemoveAllListeners();
		OfflineAccountAdminPanelPasswordField.onSubmit.RemoveAllListeners();
		OfflineAccountAdminPanelBlacklisted.onValueChanged.RemoveAllListeners();
		OfflineAccountAdminPanelServer.onValueChanged.RemoveAllListeners();
		OfflineAccountAdminPanelEmailField.onSubmit.AddListener((string value) => {GetComponent<MetaNetworkManager>().unassignedAccounts[id].email = value;File.WriteAllText(Application.persistentDataPath + "/accounts/" + GetComponent<MetaNetworkManager>().unassignedAccounts[id].uuid + ".paperwars-account", JsonUtility.ToJson(GetComponent<MetaNetworkManager>().unassignedAccounts[id]));});
		OfflineAccountAdminPanelUsernameField.onSubmit.AddListener((string value) => {GetComponent<MetaNetworkManager>().unassignedAccounts[id].username = value;File.WriteAllText(Application.persistentDataPath + "/accounts/" + GetComponent<MetaNetworkManager>().unassignedAccounts[id].uuid + ".paperwars-account", JsonUtility.ToJson(GetComponent<MetaNetworkManager>().unassignedAccounts[id]));});
		OfflineAccountAdminPanelPasswordField.onSubmit.AddListener((string value) => {GetComponent<MetaNetworkManager>().unassignedAccounts[id].password = value;File.WriteAllText(Application.persistentDataPath + "/accounts/" + GetComponent<MetaNetworkManager>().unassignedAccounts[id].uuid + ".paperwars-account", JsonUtility.ToJson(GetComponent<MetaNetworkManager>().unassignedAccounts[id]));});
		OfflineAccountAdminPanelBlacklisted.onValueChanged.AddListener((bool value) => {GetComponent<MetaNetworkManager>().unassignedAccounts[id].blacklisted = value;File.WriteAllText(Application.persistentDataPath + "/accounts/" + GetComponent<MetaNetworkManager>().unassignedAccounts[id].uuid + ".paperwars-account", JsonUtility.ToJson(GetComponent<MetaNetworkManager>().unassignedAccounts[id]));});
		OfflineAccountAdminPanelServer.onValueChanged.AddListener((bool value) => {GetComponent<MetaNetworkManager>().unassignedAccounts[id].server = value;File.WriteAllText(Application.persistentDataPath + "/accounts/" + GetComponent<MetaNetworkManager>().unassignedAccounts[id].uuid + ".paperwars-account", JsonUtility.ToJson(GetComponent<MetaNetworkManager>().unassignedAccounts[id]));});
	}

    public void BackToMainMenu() {
		HostServerCanvas.SetActive(false);
		NewMapCanvas.SetActive(false);
		JoinServerCanvas.SetActive(false);
		SettingsCanvas.SetActive(false);
		MainMenuCanvas.SetActive(true);
		MessageSending.SetActive(true);
		AdminPanel.SetActive(false);
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
		MainScript.PrintMessage("Generating World...\n(Remember to port forward the port " + GetComponent<NetworkManager>().port + " in your router settings!)");
		if (newMapNameField.text == "") {newMapNameField.text = "New Map";}
		if (newMapSeedField.text == "") {newMapSeedField.text = new System.Random().Next().ToString();}
		GetComponent<MainScript>().serverMap = GetComponent<MainScript>().GenerateMap(newMapNameField.text, int.Parse(newMapSeedField.text));
		MainScript.PrintMessage("Starting Server...\n(Remember to port forward the port " + GetComponent<NetworkManager>().port + " in your router settings!)");
		GetComponent<NetworkManager>().StartHost();
	}

    public void JoinServerConfirm(string serverName, MainScript.Version serverVersion, string ip) {
		JoinServerCanvas.SetActive(false);
		GetComponent<NetworkManager>().connectedServerName = serverName;
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
