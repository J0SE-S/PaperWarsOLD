using Riptide;
using Riptide.Utils;
using TMPro;
using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MetaNetworkManager : MonoBehaviour {
	public class ServerData {
	    public string name;
		public MainScript.Version version;
	    public string address;
		public ushort port;
	    public ushort clientId;

	    public ServerData(string serverName, MainScript.Version newVersion, string serverAddress, ushort newPort, ushort newClientId = 0) {
			name = serverName;
			version = newVersion;
			address = serverAddress;
			port = newPort;
			clientId = newClientId;
	    }
	}

	public class Account {
		public string uuid;
		public string email;
		public string username;
		public string password;
		public bool mailingList;
		public bool blacklisted;
		public bool admin;
		public bool server;

		public Account(string newUUID, string newEmail, string newUsername, string newPassword) {
			uuid = newUUID;
			email = newEmail;
			username = newUsername;
			password = newPassword;
			mailingList = false;
			blacklisted = false;
			admin = false;
			server = false;
		}
	}

	public class ClientData {
		public Account account;
		private bool blacklisted;
		public bool Blacklisted {
			get {
				if (account != null) {
					if (account.blacklisted) return true;
					else return blacklisted;
				} else return blacklisted;
			}

			set {
				blacklisted = value;
			}
		}
		public MainScript.Version version;

		public ClientData() {
			account = null;
			blacklisted = false;
			version = null;
		}
	}

	public enum MessageId : ushort {
		Version = 100,
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

	private string address;
	[SerializeField] private ushort port;
    [SerializeField] private ushort maxPlayers;
	[SerializeField] private bool JoinMainServer;
	public GameObject LoginCanvas;
	public GameObject SignupCanvas;
	public TMP_Text LoginMessage;
	public TMP_Text SignupMessage;
	public TMP_InputField LoginUsernameField;
	public TMP_InputField LoginPasswordField;
	public TMP_InputField SignupEmailField;
	public TMP_InputField SignupUsernameField;
	public TMP_InputField SignupPasswordField;
	public Toggle MailingListToggle;
	public GameObject ServerFailedConnectCanvas;
	public Scrollbar MessageSendingScrollbarVertical;
	private List<ServerData> servers;
	public GameObject ChatContent;
	public GameObject ChatText;
	public GameObject JoinServerContent;
	public GameObject ServerButton;
	public GameObject AdminButton;
	public GameObject OnlineAccountScrollViewContent;
	public GameObject OfflineAccountScrollViewContent;
	public Account localAccount;
	public Dictionary<ushort, ClientData> connectedClients;
	public List<Account> unassignedAccounts;
	public List<MainScript.Version> blacklistedVersions;

    public Server Server { get; private set; }
    public Client Client { get; private set; }

    private void Awake() {
        Singleton = this;
    }

    public void Start3() {
        Server = new Server();
        Server.ClientConnected += PlayerJoined;
	    Server.ClientDisconnected += PlayerLeft;

        Client = new Client();
        Client.Connected += DidConnect;
        Client.ConnectionFailed += FailedToConnect;
        Client.Disconnected += DidDisconnect;

	    servers = new List<ServerData>();
#if UNITY_EDITOR
		UpdateIPAddress();
		InvokeRepeating("UpdateIPAddress", 300f, 300f);
		File.WriteAllText(Application.persistentDataPath + "/.ipaddress", address);
		Directory.CreateDirectory(Application.persistentDataPath + "/accounts");
		connectedClients = new();
		unassignedAccounts = new();
		foreach (string accountDataPath in Directory.GetFiles(Application.persistentDataPath + "/accounts", "*.paperwars-account")) {
			unassignedAccounts.Add(JsonUtility.FromJson<Account>(File.ReadAllText(accountDataPath)));
		}
	    StartHost();
		address = "192.168.4.62";
	    if (JoinMainServer) {
            JoinServer();
	    }
#else
		try {
			address = new WebClient().DownloadString("https://raw.githubusercontent.com/J0SE-S/PaperWars/main/.ipaddress").Replace("\r\n", "").Replace("\n", "").Replace("\r", "");
			File.WriteAllText(Application.persistentDataPath + "/.ipaddress", address);
		} catch (Exception) {
			address = File.ReadAllText(Application.persistentDataPath + "/.ipaddress");
		}
		JoinServer();
#endif
		GetComponent<MenuScript>().Start4();
    }

    private void Update() {
        if (Server.IsRunning)
            Server.Update();
		Client.Update();
    }

	public void SendEmail(string email, string subject, string body) {
		var smtpClient = new SmtpClient("smtp.gmail.com", 587)
		{
			DeliveryMethod = SmtpDeliveryMethod.Network,
			EnableSsl = true,
			Credentials = new NetworkCredential("paperwars.mainserver@gmail.com", File.ReadAllText(Application.persistentDataPath + "/emailPassword.txt")),
		};
			
		smtpClient.Send("paperwars.mainserver@gmail.com", email, subject, body);
	}

	private void UpdateIPAddress() {
		try {
			address = new WebClient().DownloadString("http://icanhazip.com").Replace("\r\n", "").Replace("\n", "").Replace("\r", "");
		} catch (Exception) {
			address = new WebClient().DownloadString("https://api.ipify.org").Replace("\r\n", "").Replace("\n", "").Replace("\r", "");
		}
		File.WriteAllText(Directory.GetParent(Application.dataPath) + "/.ipaddress", address);
	}

	[MessageHandler((ushort)MessageId.Version)]
	private static void ProcessVersion(ushort sender, Message message) {
		MainScript.Version version = new MainScript.Version(message.GetByte(), message.GetByte(), message.GetByte(), (MainScript.Version.SubversionType) message.GetByte(), message.GetByte());
		if (Camera.main.GetComponent<MetaNetworkManager>().blacklistedVersions.Contains(version)) {
			Message disconnectMessage = Message.Create(MessageSendMode.Reliable, MessageId.Version);
			message.AddByte(0);
			Camera.main.GetComponent<MetaNetworkManager>().Server.DisconnectClient(sender, disconnectMessage);
		} else {
			Camera.main.GetComponent<MetaNetworkManager>().connectedClients[sender].version = version;
		}
	}
	public void SendChatMessage(string message) {
		Message message1 = Message.Create(MessageSendMode.Reliable, MessageId.ChatMessage);
		message1.AddString(message);
		Client.Send(message1);
	}

	[MessageHandler((ushort)MessageId.ChatMessage)]
	private static void ProcessChatMessage(ushort sender, Message message) {
		if (Camera.main.GetComponent<MetaNetworkManager>().connectedClients[sender].Blacklisted) return;
		Camera.main.GetComponent<MetaNetworkManager>().Server.SendToAll(message);
	}

	[MessageHandler((ushort)MessageId.ChatMessage)]
	private static void ProcessChatMessage(Message message) {
		Canvas.ForceUpdateCanvases();
		GameObject message1 = Instantiate(Camera.main.GetComponent<MetaNetworkManager>().ChatText);
		message1.GetComponent<TMP_Text>().text = message.GetString();
		message1.transform.SetParent(Camera.main.GetComponent<MetaNetworkManager>().ChatContent.transform, false);
		Camera.main.GetComponent<MetaNetworkManager>().MessageSendingScrollbarVertical.value = -0.01f;
		Canvas.ForceUpdateCanvases();
	}

	public void SendStartServerData(string serverName, string serverAddress, ushort serverPort) {
	    Message message = Message.Create(MessageSendMode.Reliable, MessageId.StartServer);
	    message.AddString(serverName);
		message.AddByte(GetComponent<MainScript>().currentVersion.major);
		message.AddByte(GetComponent<MainScript>().currentVersion.minor);
		message.AddByte(GetComponent<MainScript>().currentVersion.patch);
		message.AddByte((byte) GetComponent<MainScript>().currentVersion.subversionType);
		message.AddByte(GetComponent<MainScript>().currentVersion.subversion);
	    message.AddString(serverAddress);
		message.AddUShort(serverPort);
	    Client.Send(message);
	}

	public void SendStopServerData() {
	    Client.Send(Message.Create(MessageSendMode.Reliable, MessageId.StopServer));
	}

	[MessageHandler((ushort)MessageId.StartServer)]
	private static void ProcessServerStart(ushort sender, Message message) {
		if (Camera.main.GetComponent<MetaNetworkManager>().connectedClients[sender].Blacklisted || !Camera.main.GetComponent<MetaNetworkManager>().connectedClients[sender].account.server) return;
	    Camera.main.GetComponent<MetaNetworkManager>().servers.Add(new ServerData(message.GetString(), new MainScript.Version(message.GetByte(), message.GetByte(), message.GetByte(), (MainScript.Version.SubversionType) message.GetByte(), message.GetByte()), message.GetString(), message.GetUShort(), sender));
	}

	[MessageHandler((ushort)MessageId.StopServer)]
	private static void ProcessServerStop(ushort sender, Message message) {
		if (Camera.main.GetComponent<MetaNetworkManager>().connectedClients[sender].Blacklisted) return;
	    Camera.main.GetComponent<MetaNetworkManager>().servers.RemoveAll(s => s.clientId == sender);
	}

	[MessageHandler((ushort)MessageId.ServerList)]
	private static void ProcessServerListRequest(ushort sender, Message message) {
		if (Camera.main.GetComponent<MetaNetworkManager>().connectedClients[sender].Blacklisted) return;
	    message = Message.Create(MessageSendMode.Reliable, MessageId.ServerList);
	    message.AddInt(Camera.main.GetComponent<MetaNetworkManager>().servers.Count);
	    for (int i = 0; i < Camera.main.GetComponent<MetaNetworkManager>().servers.Count; i++) {
	        message.AddString(Camera.main.GetComponent<MetaNetworkManager>().servers[i].name);
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
			Camera.main.GetComponent<MetaNetworkManager>().servers.Add(new ServerData(message.GetString(), new MainScript.Version(message.GetByte(), message.GetByte(), message.GetByte(), (MainScript.Version.SubversionType) message.GetByte(), message.GetByte()), message.GetString(), message.GetUShort()));
	    }
	    Camera.main.GetComponent<MetaNetworkManager>().RefreshServerList();
	}

	public void RefreshServerList() {
	    foreach (Transform child in JoinServerContent.transform) {
     		GameObject.Destroy(child.gameObject);
 	    }
	    foreach (ServerData data in servers) {
			GameObject button = GameObject.Instantiate(ServerButton, JoinServerContent.GetComponent<Transform>());
			button.GetComponentsInChildren<TMP_Text>()[0].text = data.name + " (" + data.version + ")";
			button.GetComponent<Button>().onClick.AddListener(() => {GetComponent<MenuScript>().JoinServerConfirm(data.name, data.version, data.address);});
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
		connectedClients = new();
        Server.Start(port, maxPlayers);
    }

	public void JoinServer() {
	    ServerFailedConnectCanvas.SetActive(false);
	    MainScript.PrintMessage("Connecting to The Main Server...");
	    Client.Connect($"{address}:{port}");
	}

	public void SwitchToLoginCanvas() {
		LoginUsernameField.text = "";
		LoginPasswordField.text = "";
		LoginCanvas.SetActive(true);
		SignupCanvas.SetActive(false);
		SignupMessage.text = "";
	}

	public void SwitchToSignUpCanvas() {
		SignupEmailField.text = "";
		SignupUsernameField.text = "";
		SignupPasswordField.text = "";
		LoginCanvas.SetActive(false);
		SignupCanvas.SetActive(true);
		SignupMessage.text = "";
	}

	public void Login() {
		if (LoginUsernameField.text == "") {
			LoginMessage.text = "Please enter a username.";
			return;
		}
		if (LoginPasswordField.text == "") {
			LoginMessage.text = "Please enter a password.";
			return;
		}
		Client.Send(Message.Create(MessageSendMode.Reliable, MessageId.LoginAccount).AddString(LoginUsernameField.text).AddString(LoginPasswordField.text));
		LoginMessage.text = "";
	}

	public void SignUp() {
		if (SignupEmailField.text == "") {
			SignupMessage.text = "Please enter an email.";
			return;
		}
		if (SignupUsernameField.text == "") {
			SignupMessage.text = "Please enter a username.";
			return;
		}
		if (SignupPasswordField.text == "") {
			SignupMessage.text = "Please enter a password.";
			return;
		}
		Client.Send(Message.Create(MessageSendMode.Reliable, MessageId.RegisterAccount).AddString(SignupEmailField.text).AddString(SignupUsernameField.text).AddString(SignupPasswordField.text).AddBool(MailingListToggle.isOn));
		SignupMessage.text = "";
	}

	[MessageHandler((ushort)MessageId.LoginAccount)]
	private static void ProcessAccountLogin(ushort sender, Message message) {
		string username = message.GetString();
		string password = message.GetString();
		foreach (Account account in Camera.main.GetComponent<MetaNetworkManager>().unassignedAccounts) {
			if (username == account.username && password == account.password) {
				Camera.main.GetComponent<MetaNetworkManager>().connectedClients[sender].account = account;
				Camera.main.GetComponent<MetaNetworkManager>().Server.Send(AddAccount(Message.Create(MessageSendMode.Reliable, MessageId.LoginAccount).AddByte(0), account), sender);
				return;
			}
		}
		foreach (ClientData clientData in Camera.main.GetComponent<MetaNetworkManager>().connectedClients.Values) {
			if (clientData.account != null) {
				if (username == clientData.account.username) {
					Camera.main.GetComponent<MetaNetworkManager>().Server.Send(Message.Create(MessageSendMode.Reliable, MessageId.LoginAccount).AddByte(2), sender);
					return;
				}
			}
		}
		Camera.main.GetComponent<MetaNetworkManager>().Server.Send(Message.Create(MessageSendMode.Reliable, MessageId.LoginAccount).AddByte(1), sender);
	}

	[MessageHandler((ushort)MessageId.LoginAccount)]
	private static void ProcessAccountLoginResponse(Message message) {
		switch (message.GetByte()) {
			case 0:
				Camera.main.GetComponent<MetaNetworkManager>().localAccount = GetAccount(message);
				if (Camera.main.GetComponent<MetaNetworkManager>().localAccount.server) {
					Camera.main.GetComponent<MenuScript>().HostServerButton.gameObject.SetActive(true);
				}
				if (Camera.main.GetComponent<MetaNetworkManager>().localAccount.admin) {
					Camera.main.GetComponent<MetaNetworkManager>().AdminButton.SetActive(true);
				}
				Camera.main.GetComponent<MetaNetworkManager>().LoginCanvas.SetActive(false);
				Camera.main.GetComponent<MenuScript>().MainMenuCanvas.SetActive(true);
				Camera.main.GetComponent<MetaNetworkManager>().SendChatMessage(Camera.main.GetComponent<MetaNetworkManager>().localAccount.username + " has connected to the Main Server!");
				Camera.main.GetComponent<MetaNetworkManager>().LoginMessage.text = "";
				Camera.main.GetComponent<MetaNetworkManager>().SignupMessage.text = "";
				Camera.main.GetComponent<MenuScript>().MessageSending.SetActive(true);
				break;
			case 1:
				Camera.main.GetComponent<MetaNetworkManager>().LoginMessage.text = "Incorrect username/password!";
				break;
			case 2:
				Camera.main.GetComponent<MetaNetworkManager>().LoginMessage.text = "Account already in use!";
				break;
		}
	}

	public static Message AddAccount(Message message, Account account) {
		message.AddString(account.uuid);
		message.AddString(account.email);
		message.AddString(account.username);
		message.AddString(account.password);
		message.AddBool(account.admin);
		message.AddBool(account.server);
		message.AddBool(account.mailingList);
		return message;
	}

	public static Account GetAccount(Message message) {
		Account account = new Account(message.GetString(), message.GetString(), message.GetString(), message.GetString());
		account.admin = message.GetBool();
		account.server = message.GetBool();
		account.mailingList = message.GetBool();
		return account;
	}

	[MessageHandler((ushort)MessageId.RegisterAccount)]
	private static void ProcessAccountRegister(ushort sender, Message message) {
		string uuid;
		string email = message.GetString();
		string username = message.GetString();
		string password = message.GetString();
		bool mailingList = message.GetBool();
		do {
			uuid = System.Guid.NewGuid().ToString();
		} while (File.Exists(Application.persistentDataPath + "/accounts/" + uuid + ".paperwars-account"));
		foreach (ClientData clientData in Camera.main.GetComponent<MetaNetworkManager>().connectedClients.Values) {
			if (clientData.account != null) {
				if (username == clientData.account.username) {
					Camera.main.GetComponent<MetaNetworkManager>().Server.Send(Message.Create(MessageSendMode.Reliable, MessageId.RegisterAccount).AddByte(1), sender);
					return;
				}
			}
		}
		foreach (Account account1 in Camera.main.GetComponent<MetaNetworkManager>().unassignedAccounts) {
			if (email == account1.email) {
				Camera.main.GetComponent<MetaNetworkManager>().Server.Send(Message.Create(MessageSendMode.Reliable, MessageId.RegisterAccount).AddByte(2), sender);
				return;
			}
			if (username == account1.username) {
				Camera.main.GetComponent<MetaNetworkManager>().Server.Send(Message.Create(MessageSendMode.Reliable, MessageId.RegisterAccount).AddByte(1), sender);
				return;
			}
		}
		Account account = new Account(uuid, email, username, password);
		account.mailingList = mailingList;
		Camera.main.GetComponent<MetaNetworkManager>().unassignedAccounts.Add(account);
		File.WriteAllText(Application.persistentDataPath + "/accounts/" + uuid + ".paperwars-account", JsonUtility.ToJson(account));
		if (mailingList) {
			Camera.main.GetComponent<MetaNetworkManager>().SendEmail(email, "Welcome to PaperWars", "Hello!\n\nThank you for signing up for PaperWars! An account has been created for you using this email address and the username \"" + username + "\". You have also been signed up to the mailing list. If you wish to change the account's settings, please reply to this email.");
		} else {
			Camera.main.GetComponent<MetaNetworkManager>().SendEmail(email, "Welcome to PaperWars", "Hello!\n\nThank you for signing up for PaperWars! An account has been created for you using this email address and the username \"" + username + "\". If you wish to change the account's settings, please reply to this email.");
		}
		Camera.main.GetComponent<MetaNetworkManager>().Server.Send(Message.Create(MessageSendMode.Reliable, MessageId.RegisterAccount).AddByte(0), sender);
	}

	[MessageHandler((ushort)MessageId.RegisterAccount)]
	private static void ProcessAccountRegisterResponse(Message message) {
		switch (message.GetByte()) {
			case 0:
				Camera.main.GetComponent<MetaNetworkManager>().SignupMessage.text = "Signup Complete! Please login to your new account.";
				break;
			case 1:
				Camera.main.GetComponent<MetaNetworkManager>().SignupMessage.text = "Username already in use!";
				break;
			case 2:
				Camera.main.GetComponent<MetaNetworkManager>().SignupMessage.text = "Email already in use!";
				break;
		}
	}

    private void DidConnect(object sender, EventArgs e) {
	    MainScript.PrintMessage("Connected to The Main Server!");
	    LoginCanvas.SetActive(true);
    }

    private void FailedToConnect(object sender, ConnectionFailedEventArgs e) {
	    MainScript.PrintMessageError("Unable to connect to The Main Server!");
	    ServerFailedConnectCanvas.SetActive(true);
	}

    private void PlayerJoined(object sender, ServerConnectedEventArgs e) {
		ClientData client = new ClientData();
		connectedClients.Add(e.Client.Id, client);
	}

	public void PlayerLeft(object sender, ServerDisconnectedEventArgs e) {
	    servers.RemoveAll(s => s.clientId == e.Client.Id);
		connectedClients.Remove(e.Client.Id);
	}

    private void DidDisconnect(object sender, DisconnectedEventArgs e) {
		if (e.Message == null) {
	    	switch (e.Reason) {
				case DisconnectReason.ConnectionRejected:
					MainScript.PrintMessageError("Disconnected from The Main Server! Reason: Connection Denied.");
					break;
				case DisconnectReason.Disconnected:
					MainScript.PrintMessageError("Disconnected from The Main Server! Reason: Self-Disconnection.");
					break;
				case DisconnectReason.Kicked:
					MainScript.PrintMessageError("Disconnected from The Main Server! Reason: Connection Blocked.");
					break;
				case DisconnectReason.NeverConnected:
					MainScript.PrintMessageError("Disconnected from The Main Server! Reason: No Connection.");
					break;
				case DisconnectReason.ServerStopped:
					MainScript.PrintMessageError("Disconnected from The Main Server! Reason: Server Offline.");
					break;
				case DisconnectReason.TimedOut:
					MainScript.PrintMessageError("Disconnected from The Main Server! Reason: Connection Timed Out.");
					break;
				case DisconnectReason.TransportError:
					MainScript.PrintMessageError("Disconnected from The Main Server! Reason: Transport Error.");
					break;
				default:
					MainScript.PrintMessageError("Disconnected from The Main Server! Reason: Unknown.");
					break;
			}
		} else {
			byte reason = e.Message.GetByte();
			switch (reason) {
				case 0:
					MainScript.PrintMessageError("Disconnected from The Main Server! Reason: Blocked Version.");
					break;
				case 1:
					MainScript.PrintMessageError("Disconnected from The Main Server! Reason: Disconnected by Admin.");
					break;
				default:
					MainScript.PrintMessageError("Disconnected from The Main Server! Reason: Unknown.");
					break;
			}
		}
	    ServerFailedConnectCanvas.SetActive(true);
	}
}