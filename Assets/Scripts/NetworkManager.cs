using TMPro;
using Riptide;
using Riptide.Utils;
using System;
using System.IO;
using System.Net;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;

public class NetworkManager : MonoBehaviour {
	public enum MessageId : ushort {
		SessionID,
		ChunkData,
		Join,
        //SpawnEntity,
        EntityMovement,
		PlaceBuilding,
		Sync,
	    ChangeBlock
    }

	public class ClientData {
		public string uuid;
		public string username;
		public Coroutine sendMap;
		public Coroutine syncData;
		public bool blacklisted;
		public MainScript.Version version;

		public ClientData(Coroutine newSendMap, Coroutine newSyncData) {
			uuid = "";
			username = "";
			sendMap = newSendMap;
			syncData = newSyncData;
			blacklisted = false;
			version = null;
		}
	}
	
    private static NetworkManager _singleton;
    public static NetworkManager Singleton
	{
        get => _singleton;
        private set
        {
			if (_singleton == null)
                _singleton = value;
            else if (_singleton != value) {
#if UNITY_EDITOR
                Debug.Log($"{nameof(NetworkManager)} instance already exists, destroying object!");
#endif
                Destroy(value);
            }
        }
    }

    public ushort port;
	[SerializeField] private ushort maxPlayers;
	public GameObject GameCanvas;
	public GameObject GameGrid;
	private bool serverOffline;
	public byte serverReconnectionAttempts;
	private string connectedIp;
	private ushort connectedPort;
	public Texture2D MinimapTexture;
	public RawImage Minimap;
	public string connectedServerName;
	public string connectedServerVersion;
	public string hostedServerName;
	public string hostedServerVersion;
	public Dictionary<ushort, ClientData> connectedClients;

    public Server Server { get; private set; }
    public Client Client { get; private set; }

    private void Awake() {
        Singleton = this;
    }

    public void Start2() {
		Message.MaxPayloadSize = 2048;
#if UNITY_EDITOR
		RiptideLogger.Initialize(Debug.Log, Debug.Log, Debug.LogWarning, Debug.LogError, false);
#endif
        Server = new Server();
        Server.ClientConnected += PlayerJoined;
	    Server.ClientDisconnected += PlayerLeft;

        Client = new Client();
        Client.Connected += DidConnect;
        Client.ConnectionFailed += FailedToConnect;
        //Client.ClientDisconnected += OtherPlayerLeft;
        Client.Disconnected += DidDisconnect;

	    serverOffline = true;

	    InvokeRepeating("SaveMap", 15, 15);
	    GetComponent<MetaNetworkManager>().Start3();
    }

	private void SaveMap() {
	    if (Server.IsRunning) {
			FileStream fs = new FileStream(Application.persistentDataPath + "/.maps/" + GetComponent<MainScript>().serverMap.name + ".paperwars-map", FileMode.Create);
			BinaryFormatter formatter = new BinaryFormatter();
        	try {
            	formatter.Serialize(fs, GetComponent<MainScript>().serverMap);
        	} catch (SerializationException e) {
            	Console.WriteLine("Failed to serialize. Reason: " + e.Message);
            	throw;
			} finally {
				fs.Close();
			}
	    }
	}

	private void OnApplicationQuit() {
        File.Delete(Application.persistentDataPath + "/.temp" + GetComponent<NetworkManager>().port);
    }

    private void FixedUpdate() {
        if (Server.IsRunning) {
            Server.Update();
			GetComponent<MenuScript>().HostServerButton.interactable = false;
	        serverOffline = false;
			GetComponent<MainScript>().ServerMapFixedUpdate();
	    } else if (!serverOffline) {
			GetComponent<MetaNetworkManager>().SendStopServerData();
			GetComponent<MenuScript>().HostServerButton.interactable = true;
			serverOffline = true;
	    }
        Client.Update();
    }

    public void StartHost() {
#if UNITY_EDITOR
		port = 35723;
#else
		for (ushort i = 1; i <= 51; i++) {
			if (i == 51) {
				Application.Quit();
			}
			if (!File.Exists(Application.persistentDataPath + "/.temp" + (35724 + i))) {
				port = (ushort)(i + 35724);
				File.Create(Application.persistentDataPath + "/.temp" + (35724 + i));
				break;
			}
		}
#endif
		StartCoroutine("StartHost2");
	}

	private IEnumerator StartHost2() {
		GetComponent<MainScript>().ServerLoadedEntities = new();
		for (int row = 0; row < 40; row++) {
            for (int column = 0; column < 40; column++) {
				byte[,] chunk = new byte[25, 25];
				for (int x = 0; x < 25; x++) {
					for (int y = 0; y < 25; y++) {
						chunk[x, y] = GetComponent<MainScript>().serverMap.tileMap(row * 25 + x, column * 25 + y);
					}
				}
				GetComponent<MainScript>().serverMap.LoadServerChunk(chunk, row, column);
				yield return new WaitForSeconds(0.0001f);
			}
		}
		/*foreach (MainScript.Map.Entity entity in GetComponent<MainScript>().serverMap.entities.Values) {
			entity.ServerVisualize();
			yield return new WaitForSeconds(0.002f);
		}*/
		hostedServerName = GetComponent<MainScript>().serverMap.name;
		hostedServerVersion = GetComponent<MainScript>().currentVersion.ToString();
		GetComponent<MenuScript>().HostServerButton.interactable = false;
		MainScript.PrintMessage("Server Started!");
		connectedClients = new();
		Server.Start(port, maxPlayers);
		if (GetComponent<MainScript>().settings.joinServerOnHost) {
			JoinGame("127.0.0.1", port);
		} else {
			GetComponent<MenuScript>().BackToMainMenu();
		}
		GetComponent<MenuScript>().newMapNameField.interactable = true;
		GetComponent<MenuScript>().newMapSeedField.interactable = true;
		GetComponent<MenuScript>().createMapButton.interactable = true;
		GetComponent<MenuScript>().newMapBackButton.interactable = true;
		string address;
		try {
			address = new WebClient().DownloadString("http://icanhazip.com").Replace("\r\n", "").Replace("\n", "").Replace("\r", "");
		} catch (Exception) {
			address = new WebClient().DownloadString("https://api.ipify.org").Replace("\r\n", "").Replace("\n", "").Replace("\r", "");
		}
		GetComponent<MetaNetworkManager>().SendStartServerData(hostedServerName, address, port);
		GetComponent<MetaNetworkManager>().SendChatMessage(GetComponent<MetaNetworkManager>().localAccount.username + " has started the server \"" + hostedServerName + " (" + hostedServerVersion + ")" + "\".");
	}

    public void JoinGame(string ip, ushort portNum) {
	    MainScript.PrintMessage("Joining Server...");
        Client.Connect($"{ip}:{portNum}");
	    connectedIp = ip;
		connectedPort = portNum;
    }

    public void LeaveGame() {
		serverReconnectionAttempts = 255;
        Client.Disconnect();
	    GetComponent<MainScript>().nonSolidTilemap.ClearAllTiles();
	    GetComponent<MainScript>().solidTilemap.ClearAllTiles();
    }

    private void DidConnect(object sender, EventArgs e) {
	    serverReconnectionAttempts = 0;
	    MainScript.PrintMessage("Joined Server!");
	    GameCanvas.SetActive(true);
		GetComponent<MenuScript>().MessageSending.SetActive(true);
		Client.Send(Message.Create(MessageSendMode.Reliable, MessageId.SessionID).AddInt(GetComponent<MetaNetworkManager>().sessionID));
		GetComponent<MetaNetworkManager>().SendChatMessage(GetComponent<MetaNetworkManager>().localAccount.username + " has connected to server \"" + connectedServerName + " (" + connectedServerVersion + ")" + "\".");
    }

    private void FailedToConnect(object sender, EventArgs e) {
	    connectedIp = "";
		connectedPort = 0;
	}

    private void PlayerJoined(object sender, ServerConnectedEventArgs e) {
		connectedClients.Add(e.Client.Id, new ClientData(StartCoroutine("SendMapData", e.Client.Id), StartCoroutine("SyncData")));
	}

	private void PlayerLeft(object sender, ServerDisconnectedEventArgs e) {
	    StopCoroutine(connectedClients[e.Client.Id].sendMap);
		StopCoroutine(connectedClients[e.Client.Id].syncData);
		Destroy(GetComponent<MainScript>().serverMap.entities[connectedClients[e.Client.Id].uuid].visualization);
		GetComponent<MainScript>().serverMap.entities.Remove(connectedClients[e.Client.Id].uuid);
		connectedClients.Remove(e.Client.Id);
	}

	public IEnumerator SendMapData(ushort id) {
		Message message1 = Message.Create(MessageSendMode.Reliable, MessageId.ChunkData);
    	message1.AddByte(0);
    	message1.AddByte(0);
		message1.AddFloat(GetComponent<MainScript>().serverMap.currentTime);
    	for (int x = 0; x < 25; x++) {
        	for (int y = 0; y < 25; y++) {
    	    	message1.AddByte(GetComponent<MainScript>().serverMap.tileMap(x, y));
        	}
		}
		Server.Send(message1, id);
		foreach (MainScript.Map.Entity entity in GetComponent<MainScript>().serverMap.entities.Values) {
	    	Message message = Message.Create(MessageSendMode.Reliable, MessageId.SpawnEntity);
			string type = entity.GetType().FullName;
			message.AddString(entity.uuid);
			switch (type) {
				case "MainScript+Map+Entity+Stickman":
					message.AddByte(0);
					message.AddFloat(entity.Position.x);
					message.AddFloat(entity.Position.y);
					message.AddString((entity as MainScript.Map.Entity.Stickman).displayName);
					break;
				case "MainScript+Map+Entity+Airship":
					message.AddByte(1);
					message.AddFloat(entity.Position.x);
					message.AddFloat(entity.Position.y);
					break;
				default:
					message.AddByte(255);
					break;
			}
			Server.Send(message, id);
			yield return new WaitForSeconds(0.01F);
	    }
		Server.Send(Message.Create(MessageSendMode.Reliable, MessageId.Join).AddByte(0), id);
	}

	public IEnumerator SyncData() {
		Message message;
		for (;;) {
			yield return new WaitForSeconds(0.5f);
			message = Message.Create(MessageSendMode.Unreliable, MessageId.Sync);
			message.AddFloat(GetComponent<MainScript>().serverMap.currentTime);
			Server.SendToAll(message);
		}
	}

    //private void OtherPlayerLeft(object sender, ClientDisconnectedEventArgs e) {}

    private void DidDisconnect(object sender, DisconnectedEventArgs e) {
	    GetComponent<MainScript>().nonSolidTilemap.ClearAllTiles();
	    GetComponent<MainScript>().solidTilemap.ClearAllTiles();
		foreach (GameObject entity in GetComponent<MainScript>().LoadedEntities.Values) {
			Destroy(entity);
		}
		GetComponent<MainScript>().LoadedEntities = new();
	    if (serverReconnectionAttempts > 5) {
	        MainScript.PrintMessage("Disconnected.");
	        GameCanvas.SetActive(false);
			connectedIp = "";
			connectedPort = 0;
			GetComponent<MenuScript>().BackToMainMenu();
	    } else {
			serverReconnectionAttempts++;
			JoinGame(connectedIp, connectedPort);
	    }
		GetComponent<MetaNetworkManager>().SendChatMessage(GetComponent<MetaNetworkManager>().localAccount.username + " has disconnected from server \"" + connectedServerName + " (" + connectedServerVersion + ")" + "\".");
	}

	private static Color32 ToColor(byte color) {
	    switch (color) {
			//stone
			case 0:
				return new Color32(156, 156, 156, 255);
			case 1:
				return new Color32(156, 156, 156, 255);
			case 2:
				return new Color32(156, 156, 156, 255);
			case 3:
				return new Color32(156, 156, 156, 255);
			case 4:
				return new Color32(156, 156, 156, 255);
			case 5:
				return new Color32(156, 156, 156, 255);
			case 6:
				return new Color32(156, 156, 156, 255);
			case 7:
				return new Color32(156, 156, 156, 255);
			//dirt
			case 8:
				return new Color32(128, 107, 65, 255);
			//grass
			case 9:
				return new Color32(0, 230, 69, 255);
			case 10:
				return new Color32(0, 230, 69, 255);
			case 11:
				return new Color32(0, 230, 69, 255);
			case 12:
				return new Color32(0, 230, 69, 255);
			case 13:
				return new Color32(0, 230, 69, 255);
			//water
			case 14:
				return new Color32(102, 204, 255, 255);
			case 15:
				return new Color32(0, 179, 255, 255);
			case 16:
				return new Color32(0, 156, 222, 255);
			//sand
			case 17:
				return new Color32(194, 178, 128, 255);
			default:
				return new Color32(0, 0, 0, 255);
	    }
	}

	[MessageHandler((ushort)MessageId.SessionID)]
	private static void ProcessSessionId(ushort sender, Message message) {
		Message message1 = Message.Create(MessageSendMode.Reliable, MetaNetworkManager.MessageId.AccountData).AddInt(message.GetInt());
		Camera.main.GetComponent<MetaNetworkManager>().Client.Send(message1.AddUShort(sender));
	}

	[MessageHandler((ushort)MessageId.Sync)]
	private static void ProcessSyncedData(Message message) {
		Camera.main.GetComponent<MainScript>().currentTime = message.GetFloat();
	}

	[MessageHandler((ushort)MessageId.Join)]
	private static void ProcessPlayerJoin(Message message) {
		if (message.GetByte() == 0) {
			Camera.main.GetComponent<MainScript>().buildingPlacementBlueprint = Instantiate(Camera.main.GetComponent<MainScript>().BuildingBlueprints[0]);
			Camera.main.GetComponent<MainScript>().buildingPlacementMode = true;
			Camera.main.GetComponent<MainScript>().buildingPlacementType = 0;
			Camera.main.GetComponent<PlayerController>().Player = Camera.main.gameObject;
		}
	}

	[MessageHandler((ushort)MessageId.Join)]
	private static void ProcessPlayerJoin(ushort sender, Message message) {
		string uuid = Camera.main.GetComponent<NetworkManager>().connectedClients[sender].uuid;
		var x = message.GetFloat();
		var y = message.GetFloat();
		MainScript.Map.Entity.Airship entity = new MainScript.Map.Entity.Airship(new Vector2(x, y), System.Guid.NewGuid().ToString());
		MainScript.Map.Entity.Stickman entity1 = new MainScript.Map.Entity.Stickman(new Vector2(x, y), 100f, Camera.main.GetComponent<MetaNetworkManager>().connectedClients[sender].account.username, uuid, new AI.Null());
		Camera.main.GetComponent<MainScript>().serverMap.entities.Add(entity.uuid, entity);
		Camera.main.GetComponent<MainScript>().serverMap.entities.Add(uuid, entity1);
		Message message1 = Message.Create(MessageSendMode.Reliable, MessageId.SpawnEntity);
		message1.AddString(uuid);
		message1.AddByte(4);
		message1.AddFloat(entity1.Position.x);
		message1.AddFloat(entity1.Position.y);
		message1.AddString(Camera.main.GetComponent<NetworkManager>().connectedClients[sender].username);
		message1.AddBool(true);
		Camera.main.GetComponent<NetworkManager>().Server.Send(message1, sender);
		//Camera.main.GetComponent<NetworkManager>().Server.Send(Message.Create(MessageSendMode.Reliable, MessageId.Join).AddByte(1), sender);
	}

	[MessageHandler((ushort)MessageId.ChunkData)]
	private static void ProcessChunkData(Message message) {
	    byte row = message.GetByte();
	    byte column = message.GetByte();
	    byte data;
	    byte[,] chunk = new byte[25, 25];
		if (row == 0 && column == 0) {
			Camera.main.GetComponent<NetworkManager>().MinimapTexture = new Texture2D(1000, 1000);
			Camera.main.GetComponent<MainScript>().currentTime = message.GetFloat();
		}
	    for (int x = 0; x < 25; x++) {
            for (int y = 0; y < 25; y++) {
		    	data = message.GetByte();
                chunk[x, y] = data;
		    	if (data >= 64) data -= 64;
				Camera.main.GetComponent<NetworkManager>().MinimapTexture.SetPixel(row*25+x,column*25+y,ToColor(data));
            }
        }
		Camera.main.GetComponent<NetworkManager>().MinimapTexture.Apply();
		Camera.main.GetComponent<NetworkManager>().Minimap.texture = Camera.main.GetComponent<NetworkManager>().MinimapTexture;
	    Camera.main.GetComponent<MainScript>().LoadChunk(chunk, row, column);
		Camera.main.GetComponent<NetworkManager>().Client.Send(Message.Create(MessageSendMode.Reliable, MessageId.ChunkData).AddByte(row).AddByte(column));
	}

	[MessageHandler((ushort)MessageId.ChunkData)]
	private static void ProcessChunkDataResponse(ushort sender, Message message) {
		byte row = message.GetByte();
	    byte column = message.GetByte();
		if (column >= 39) {
			column = 0;
			if (row >= 39) {
				return;
			} else {
				row++;
			}
		} else {
			column++;
		}
		message = Message.Create(MessageSendMode.Reliable, MessageId.ChunkData).AddByte(row).AddByte(column);
		for (int x = 0; x < 25; x++) {
        	for (int y = 0; y < 25; y++) {
    	    	message.AddByte(Camera.main.GetComponent<MainScript>().serverMap.tileMap(x+row*25, y+column*25));
        	}
		}
		Camera.main.GetComponent<NetworkManager>().Server.Send(message, sender);
	}

	//[MessageHandler((ushort)MessageId.SpawnEntity)]
	private static void ProcessEntityData(Message message) {
		string uuid = message.GetString();
		byte type = message.GetByte();
		switch (type) {
			case 0:
				new MainScript.Map.Entity.Stickman(new Vector2(message.GetFloat(), message.GetFloat()), message.GetString(), uuid).Visualize();
				if (message.GetBool()) {
					Camera.main.GetComponent<PlayerController>().Player = Camera.main.GetComponent<MainScript>().LoadedEntities[Camera.main.GetComponent<MetaNetworkManager>().localAccount.uuid];
				}
				break;
			case 1:
				new MainScript.Map.Entity.Airship(new Vector2(message.GetFloat(), message.GetFloat()), uuid).Visualize();
				break;
		}
	}

	[MessageHandler((ushort)MessageId.EntityMovement)]
	private static void ProcessEntityMovement(ushort sender, Message message) {
		var entity = Camera.main.GetComponent<MainScript>().serverMap.entities[Camera.main.GetComponent<NetworkManager>().connectedClients[sender].uuid];
		entity.Position = new Vector3(message.GetFloat(), message.GetFloat(), entity.Position.z);
	}

	[MessageHandler((ushort)MessageId.PlaceBuilding)]
	private static void ProcessPlaceBuildingRequest(ushort sender, Message message) {
		Message message1 = Message.Create(MessageSendMode.Reliable, MessageId.SpawnEntity);
		string uuid = System.Guid.NewGuid().ToString();
		switch (message.GetByte()) {
			case 0:
				MainScript.Map.Entity.Airship entity = new MainScript.Map.Entity.Airship(new Vector2(message.GetFloat(), message.GetFloat()), uuid);
				message1.AddByte(5);
				message1.AddFloat(entity.Position.x);
				message1.AddFloat(entity.Position.y);
				entity.ServerVisualize();
				break;
		}
		Camera.main.GetComponent<NetworkManager>().Server.SendToAll(message1);
	}
}