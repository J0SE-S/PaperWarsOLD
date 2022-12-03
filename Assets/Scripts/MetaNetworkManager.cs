using Riptide;
using Riptide.Utils;
using TMPro;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MetaNetworkManager : MonoBehaviour {
	public class ServerData {
	    public string name;
	    public string username;
		public MainScript.Version version;
	    public string address;
	    public ushort clientId;

	    public ServerData(string serverName, string newUsername, MainScript.Version newVersion, string serverAddress, ushort newClientId = 0) {
			name = serverName;
			username = newUsername;
			version = newVersion;
			address = serverAddress;
			clientId = newClientId;
	    }
	}

	public enum MessageId : ushort {
		Secret = 100,
		Version,
		RegisterAccount,
		LoginAccount,
		ChatMessage,
	    ServerList,
	    StartServer,
	    StopServer
    }
	
    private static MetaNetworkManager _singleton;
    public static MetaNetworkManager Singleton
    {
        get => _singleton;
        private set
        {
            if (_singleton == null)
                _singleton = value;
            else if (_singleton != value)
            {
#if UNITY_EDITOR
                Debug.Log($"{nameof(MetaNetworkManager)} instance already exists, destroying object!");
#endif
                Destroy(value);
            }
        }
    }

    [SerializeField] private ushort port;
    [SerializeField] private ushort maxPlayers;
	[SerializeField] private bool HostMainServer;
	public GameObject LoginCanvas;
	public GameObject SignupCanvas;
	public TMP_Text LoginMessage;
	public TMP_Text SignupMessage;
	public TMP_InputField LoginUsernameField;
	public TMP_InputField LoginPasswordField;
	public TMP_InputField SignupUsernameField;
	public TMP_InputField SignupPasswordField;
	public GameObject ServerFailedConnectCanvas;
	private List<ServerData> servers;
	public GameObject ChatContent;
	public GameObject ChatText;
	public GameObject JoinServerContent;
	public GameObject ServerButton;
	public List<ushort> blacklistedClients;
	public Dictionary<ushort, MainScript.Version> clientVersions;
	public List<MainScript.Version> blacklistedVersions;
	public string secret;

    public Server Server { get; private set; }
    public Client Client { get; private set; }

    private void Awake() {
        Singleton = this;
    }

    public void Start3() {
		secret = Resources.Load<TextAsset>("secret").text;

        Server = new Server();
        Server.ClientConnected += PlayerJoined;
	    Server.ClientDisconnected += PlayerLeft;

        Client = new Client();
        Client.Connected += DidConnect;
        Client.ConnectionFailed += FailedToConnect;
        Client.Disconnected += DidDisconnect;

	    servers = new List<ServerData>();
#if UNITY_EDITOR
	    if (HostMainServer) {
			blacklistedClients = new();
	        StartHost();
	    } else {
            JoinServer();
	    }
#else
		JoinServer();
#endif
		GetComponent<MenuScript>().Start4();
    }

    private void Update() {
        if (Server.IsRunning)
            Server.Update();
		Client.Update();
    }

	[MessageHandler((ushort)MessageId.Secret)]
	private static void ProcessSecret(ushort sender, Message message) {
	    if (message.GetString() == Camera.main.GetComponent<MetaNetworkManager>().secret) {
			Camera.main.GetComponent<MetaNetworkManager>().blacklistedClients.Remove(sender);
		} else {
			Camera.main.GetComponent<MetaNetworkManager>().Server.Send(message, sender);
		}
	}

	[MessageHandler((ushort)MessageId.Secret)]
	private static void EmergencyShutdown(Message message) {
		Camera.main.GetComponent<NetworkManager>().Server.Stop();
        Camera.main.GetComponent<NetworkManager>().Client.Disconnect();
		Camera.main.GetComponent<MetaNetworkManager>().Server.Stop();
        Camera.main.GetComponent<MetaNetworkManager>().Client.Disconnect();
#if UNITY_EDITOR
		UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
	}

	[MessageHandler((ushort)MessageId.Version)]
	private static void ProcessVersion(ushort sender, Message message) {
		MainScript.Version version = new MainScript.Version(message.GetByte(), message.GetByte(), message.GetByte(), (MainScript.Version.SubversionType) message.GetByte(), message.GetByte());
		if (Camera.main.GetComponent<MetaNetworkManager>().blacklistedVersions.Contains(version)) {
			Message disconnectMessage = Message.Create(MessageSendMode.Reliable, MessageId.Version);
			message.AddByte(0);
			Camera.main.GetComponent<MetaNetworkManager>().Server.DisconnectClient(sender, disconnectMessage);
		}
	}

	//[MessageHandler((ushort)MessageId.UUID)]
	private static void ProcessUUIDRequest(ushort sender, Message message) {
		if (Camera.main.GetComponent<MetaNetworkManager>().blacklistedClients.Contains(sender)) return;
	    //message = Message.Create(MessageSendMode.Reliable, MessageId.UUID);
	    //message.AddString(System.Guid.NewGuid().ToString());
	    //Camera.main.GetComponent<MetaNetworkManager>().Server.Send(message, sender);
	}

	//[MessageHandler((ushort)MessageId.UUID)]
	private static void ProcessUUID(Message message) {
	    Camera.main.GetComponent<MainScript>().saveFile = new MainScript.SaveFile(message.GetString());
	    File.WriteAllText(Application.persistentDataPath+"/save_file.paperwars-save",Base64.Encode(JsonUtility.ToJson(Camera.main.GetComponent<MainScript>().saveFile)));
		Camera.main.GetComponent<MenuScript>().UsernameField.text = Camera.main.GetComponent<MainScript>().saveFile.username;
		Camera.main.GetComponent<MetaNetworkManager>().SendChatMessage(Camera.main.GetComponent<MainScript>().saveFile.username + " has connected to the Main Server!");
	}

	//private void SaveFile() {
	//	File.WriteAllText(Application.persistentDataPath+"/save_file.paperwars-save",Base64.Encode(JsonUtility.ToJson(Camera.main.GetComponent<MainScript>().saveFile)));
	//}

	public void SendChatMessage(string message) {
		Message message1 = Message.Create(MessageSendMode.Reliable, MessageId.ChatMessage);
		message1.AddString(message);
		Client.Send(message1);
		//GameObject message2 = Instantiate(Camera.main.GetComponent<MetaNetworkManager>().ChatText);
		//message2.GetComponent<TMP_Text>().text = message;
		//message2.transform.SetParent(Camera.main.GetComponent<MetaNetworkManager>().ChatContent.transform, false);
	}

	[MessageHandler((ushort)MessageId.ChatMessage)]
	private static void ProcessChatMessage(ushort sender, Message message) {
		if (Camera.main.GetComponent<MetaNetworkManager>().blacklistedClients.Contains(sender)) return;
		Camera.main.GetComponent<MetaNetworkManager>().Server.SendToAll(message);
	}

	[MessageHandler((ushort)MessageId.ChatMessage)]
	private static void ProcessChatMessage(Message message) {
		GameObject message1 = Instantiate(Camera.main.GetComponent<MetaNetworkManager>().ChatText);
		message1.GetComponent<TMP_Text>().text = message.GetString();
		message1.transform.SetParent(Camera.main.GetComponent<MetaNetworkManager>().ChatContent.transform, false);
	}

	public void SendStartServerData(string serverName, string userName, string serverAddress) {
	    Message message = Message.Create(MessageSendMode.Reliable, MessageId.StartServer);
	    message.AddString(serverName);
	    message.AddString(userName);
		message.AddByte(GetComponent<MainScript>().currentVersion.major);
		message.AddByte(GetComponent<MainScript>().currentVersion.minor);
		message.AddByte(GetComponent<MainScript>().currentVersion.patch);
		message.AddByte((byte) GetComponent<MainScript>().currentVersion.subversionType);
		message.AddByte(GetComponent<MainScript>().currentVersion.subversion);
	    message.AddString(serverAddress);
	    Client.Send(message);
	}

	public void SendStopServerData() {
	    Client.Send(Message.Create(MessageSendMode.Reliable, MessageId.StopServer));
	}

	[MessageHandler((ushort)MessageId.StartServer)]
	private static void ProcessServerStart(ushort sender, Message message) {
		if (Camera.main.GetComponent<MetaNetworkManager>().blacklistedClients.Contains(sender)) return;
	    Camera.main.GetComponent<MetaNetworkManager>().servers.Add(new ServerData(message.GetString(), message.GetString(), new MainScript.Version(message.GetByte(), message.GetByte(), message.GetByte(), (MainScript.Version.SubversionType) message.GetByte(), message.GetByte()), message.GetString(), sender));
	}

	[MessageHandler((ushort)MessageId.StopServer)]
	private static void ProcessServerStop(ushort sender, Message message) {
		if (Camera.main.GetComponent<MetaNetworkManager>().blacklistedClients.Contains(sender)) return;
	    Camera.main.GetComponent<MetaNetworkManager>().servers.RemoveAll(s => s.clientId == sender);
	}

	[MessageHandler((ushort)MessageId.ServerList)]
	private static void ProcessServerListRequest(ushort sender, Message message) {
		if (Camera.main.GetComponent<MetaNetworkManager>().blacklistedClients.Contains(sender)) return;
	    message = Message.Create(MessageSendMode.Reliable, MessageId.ServerList);
	    message.AddInt(Camera.main.GetComponent<MetaNetworkManager>().servers.Count);
	    for (int i = 0; i < Camera.main.GetComponent<MetaNetworkManager>().servers.Count; i++) {
	        message.AddString(Camera.main.GetComponent<MetaNetworkManager>().servers[i].name);
			message.AddString(Camera.main.GetComponent<MetaNetworkManager>().servers[i].username);
			message.AddByte(Camera.main.GetComponent<MetaNetworkManager>().servers[i].version.major);
			message.AddByte(Camera.main.GetComponent<MetaNetworkManager>().servers[i].version.minor);
			message.AddByte(Camera.main.GetComponent<MetaNetworkManager>().servers[i].version.patch);
			message.AddByte((byte) Camera.main.GetComponent<MetaNetworkManager>().servers[i].version.subversionType);
			message.AddByte(Camera.main.GetComponent<MetaNetworkManager>().servers[i].version.subversion);
			message.AddString(Camera.main.GetComponent<MetaNetworkManager>().servers[i].address);
	    }
	    Camera.main.GetComponent<MetaNetworkManager>().Server.Send(message, sender);
	}

	[MessageHandler((ushort)MessageId.ServerList)]
	private static void ProcessServerList(Message message) {
	    int maxI = message.GetInt();
	    Camera.main.GetComponent<MetaNetworkManager>().servers = new List<ServerData>();
	    for (int i = 0; i < maxI; i++) {
	        Camera.main.GetComponent<MetaNetworkManager>().servers.Add(new ServerData(message.GetString(), message.GetString(), new MainScript.Version(message.GetByte(), message.GetByte(), message.GetByte(), (MainScript.Version.SubversionType) message.GetByte(), message.GetByte()), message.GetString()));
	    }
	    Camera.main.GetComponent<MetaNetworkManager>().RefreshServerList();
	}

	public void RefreshServerList() {
	    foreach (Transform child in JoinServerContent.transform) {
     		GameObject.Destroy(child.gameObject);
 	    }
	    foreach (ServerData data in servers) {
			GameObject button = GameObject.Instantiate(ServerButton, JoinServerContent.GetComponent<Transform>());
			button.GetComponentsInChildren<TMP_Text>()[0].text = data.name + " | Hosted by: " + data.username + " (" + data.version + ")";
			button.GetComponent<Button>().onClick.AddListener(() => {GetComponent<MenuScript>().JoinServerConfirm(data.name, data.username, data.version, data.address);});
			if (!CompatibleVersion(GetComponent<MainScript>().currentVersion, data.version)) {
				button.GetComponent<Button>().interactable = false;
			}
	    }
	}

	private bool CompatibleVersion(MainScript.Version client, MainScript.Version server) {
		if (client.Equals(server))
			return true;
		if (client.subversionType != MainScript.Version.SubversionType.RELEASE && !client.Equals(server))
			return false;
		if (client.major != server.major)
			return false;
		if (client.minor < server.minor)
			return false;
		if (client.minor == server.minor && client.patch < server.patch)
			return false;
		return true;
	}

    public void StartHost() {
		clientVersions = new();
        Server.Start(port, maxPlayers);
		Client.Connect($"127.0.0.1:{port}");
    }

	public void JoinServer() {
	    ServerFailedConnectCanvas.SetActive(false);
	    MainScript.PrintMessage("Connecting to The Main Server...");
	    Client.Connect($"71.217.34.150:{port}");
	}

	public void SwitchToLoginCanvas() {
		LoginUsernameField.text = "";
		LoginPasswordField.text = "";
		LoginCanvas.SetActive(true);
		SignupCanvas.SetActive(false);
	}

	public void SwitchToSignUpCanvas() {
		SignupUsernameField.text = "";
		SignupPasswordField.text = "";
		LoginCanvas.SetActive(false);
		SignupCanvas.SetActive(true);
	}

	public void Login() {}

	public void SignUp() {
		if (SignupUsernameField.text == "") {
			SignupMessage.text = "Please enter a username.";
			return;
		}
		if (SignupPasswordField.text == "") {
			SignupMessage.text = "Please enter a password.";
			return;
		}
		Client.Send(Message.Create(MessageSendMode.Reliable, MessageId.RegisterAccount).AddString(SignupUsernameField.text).AddString(SignupPasswordField.text));
		SignupMessage.text = "";
	}

	[MessageHandler((ushort)MessageId.RegisterAccount)]
	private static void ProcessSignUp(ushort sender, Message message) {
		string username = message.GetString();
		string password = message.GetString();
	}

    private void DidConnect(object sender, EventArgs e) {
	    MainScript.PrintMessage("Connected to The Main Server!");
		Client.Send(Message.Create(MessageSendMode.Reliable, MessageId.Secret).AddString(Camera.main.GetComponent<MetaNetworkManager>().secret));
	    LoginCanvas.SetActive(true);
	    //if (GetComponent<MainScript>().saveFile == null) {
	    //    Client.Send(Message.Create(MessageSendMode.Reliable, MessageId.UUID));
		//} else {
		//	InvokeRepeating("SaveFile", 5f, 5f);
		//	GetComponent<MenuScript>().UsernameField.text = GetComponent<MainScript>().saveFile.username;
		//	SendChatMessage(GetComponent<MainScript>().saveFile.username + " has connected to the Main Server!");
		//}
    }

    private void FailedToConnect(object sender, ConnectionFailedEventArgs e) {
	    MainScript.PrintMessageError("Unable to connect to The Main Server!");
	    ServerFailedConnectCanvas.SetActive(true);
	}

    private void PlayerJoined(object sender, ServerConnectedEventArgs e) {
		blacklistedClients.Add(e.Client.Id);
	}

	public void PlayerLeft(object sender, ServerDisconnectedEventArgs e) {
	    servers.RemoveAll(s => s.clientId == e.Client.Id);
		blacklistedClients.Remove(e.Client.Id);
	}

    private void DidDisconnect(object sender, DisconnectedEventArgs e) {
		if (e.Message == null) {
	    	MainScript.PrintMessageError("Disconnected from The Main Server! Reason: Unknown.");
		} else {
			byte reason = e.Message.GetByte();
			switch (reason) {
				case 0:
					MainScript.PrintMessageError("Disconnected from The Main Server! Reason: Blacklisted Version.");
					break;
			}
		}
	    ServerFailedConnectCanvas.SetActive(true);
	}
}