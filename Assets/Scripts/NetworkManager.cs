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
using Unity.IO.Compression;
using Unity.Jobs;
using Unity.Collections;

public class NetworkManager : MonoBehaviour {
	public enum MessageId : ushort {
		ChunkData,
		Join,
        SpawnEntity,
        EntityMovement,
		PlaceBuilding,
	    ChangeBlock
    }

	/*public struct SendMapJob : IJob {
		[DeallocateOnJobCompletion]
		public NativeArray<byte> tileMap;
		public NativeArray<byte> compressedTileMap;

		public void Execute() {
			MemoryStream uncompressedDataStream = new MemoryStream(tileMap.ToArray());
			MemoryStream compressedDataStream = new MemoryStream();
			GZipStream compressor = new GZipStream(compressedDataStream, CompressionMode.Compress);
			uncompressedDataStream.CopyTo(compressor);
			NativeArray<byte> temp = new NativeArray<byte>(compressedDataStream.ToArray(), Allocator.Temp);
			compressedTileMap = temp;
			tileMap.Dispose();
			temp.Dispose();
		}
	}*/
	
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
	public Texture2D MinimapTexture;
	public RawImage Minimap;
	public string connectedServerName;
	public string connectedServerHostName;
	public string connectedServerVersion;
	public string hostedServerName;
	public string hostedServerHostName;
	public string hostedServerVersion;
	public Dictionary<ushort, Coroutine> sendMaps;
	public Dictionary<ushort, string> playerEntities; 

    public Server Server { get; private set; }
    public Client Client { get; private set; }

    private void Awake() {
        Singleton = this;
    }

    public void Start2() {
		for (ushort i = 1; i <= 51; i++) {
			if (i == 51) {
				Application.Quit();
			}
			if (!File.Exists(Application.persistentDataPath + "/temp" + (35724 + i))) {
				port = (ushort)(i + 35724);
				File.Create(Application.persistentDataPath + "/temp" + (35724 + i));
				break;
			}
		}
		sendMaps = new();
	    RiptideLogger.Initialize(Debug.Log, Debug.Log, Debug.LogWarning, Debug.LogError, false);

        Server = new Server();
        Server.ClientConnected += PlayerJoined;
	    Server.ClientDisconnected += PlayerLeft;

        Client = new Client();
        Client.Connected += DidConnect;
        Client.ConnectionFailed += FailedToConnect;
        Client.ClientDisconnected += OtherPlayerLeft;
        Client.Disconnected += DidDisconnect;

	    serverOffline = true;

	    InvokeRepeating("SaveMap", 15, 15);
	    GetComponent<MetaNetworkManager>().Start3();
    }

	private void SaveMap() {
	    if (Server.IsRunning) {
			FileStream fs = new FileStream(Application.persistentDataPath + "/maps/" + GetComponent<MainScript>().serverMap.name + ".paperwars-map", FileMode.Create);
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
        File.Delete(Application.persistentDataPath + "/temp" + GetComponent<NetworkManager>().port);
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
				yield return new WaitForSeconds(0.005f);
			}
		}
		foreach (MainScript.Map.Entity entity in GetComponent<MainScript>().serverMap.entities.Values) {
			entity.ServerVisualize();
			yield return new WaitForSeconds(0.002f);
		}
		hostedServerName = GetComponent<MainScript>().serverMap.name;
		hostedServerHostName = GetComponent<MetaNetworkManager>().localAccount.username;
		hostedServerVersion = GetComponent<MainScript>().currentVersion.ToString();
		GetComponent<MenuScript>().HostServerButton.interactable = false;
		MainScript.PrintMessage("Server Started!");
		playerEntities = new();
		if (GetComponent<MainScript>().settings.joinServerOnHost) {
			JoinGame("127.0.0.1");
		} else {
			GetComponent<MenuScript>().BackToMainMenu();
		}
		GetComponent<MenuScript>().newMapNameField.interactable = true;
		GetComponent<MenuScript>().newMapSeedField.interactable = true;
		GetComponent<MenuScript>().createMapButton.interactable = true;
		GetComponent<MenuScript>().newMapBackButton.interactable = true;
		Server.Start(port, maxPlayers);
		MainScript.PrintMessage("Server Started!");
		GetComponent<MetaNetworkManager>().SendStartServerData(hostedServerName, hostedServerHostName, new WebClient().DownloadString("http://icanhazip.com").Replace("\r\n", "").Replace("\n", "").Replace("\r", ""));
		GetComponent<MetaNetworkManager>().SendChatMessage(GetComponent<MetaNetworkManager>().localAccount.username + " has started the server \"" + hostedServerName + " | Hosted by: " + hostedServerHostName + " (" + hostedServerVersion + ")" + "\".");
	}

    public void JoinGame(string ip) {
	    MainScript.PrintMessage("Joining Server...");
        Client.Connect($"{ip}:{port}");
	    connectedIp = ip;
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
		GetComponent<MetaNetworkManager>().SendChatMessage(GetComponent<MetaNetworkManager>().localAccount.username + " has connected to server \"" + connectedServerName + " | Hosted by: " + connectedServerHostName + " (" + connectedServerVersion + ")" + "\".");
    }

    private void FailedToConnect(object sender, EventArgs e) {
	    connectedIp = "";
	}

    private void PlayerJoined(object sender, ServerConnectedEventArgs e) {
		sendMaps.Add(e.Client.Id, StartCoroutine("SendMapData", e.Client.Id));
	}

	private void PlayerLeft(object sender, ServerDisconnectedEventArgs e) {
	    StopCoroutine(sendMaps[e.Client.Id]);
		sendMaps.Remove(e.Client.Id);
	}

	public IEnumerator SendMapData(ushort id) {
	    for (int row = 0; row < 40; row++) {
			for (int column = 0; column < 40; column++) {
	            Message message = Message.Create(MessageSendMode.Reliable, MessageId.ChunkData);
		    	message.AddInt(row);
		    	message.AddInt(column);
		    	for (int x = 0; x < 25; x++) {
		        	for (int y = 0; y < 25; y++) {
		    	    	message.AddByte(GetComponent<MainScript>().serverMap.tileMap(x+row*25,y+column*25));
		        	}
		    	}
		    	Server.Send(message, id);
	            yield return new WaitForSeconds(0.05F);
			}
	    }
		foreach (MainScript.Map.Entity entity in GetComponent<MainScript>().serverMap.entities.Values) {
	    	Message message = Message.Create(MessageSendMode.Reliable, MessageId.SpawnEntity);
			string type = entity.GetType().FullName;
			message.AddString(entity.uuid);
			switch (type) {
				case "MainScript+Map+Entity+Tree":
					message.AddByte(0);
					message.AddFloat(entity.Position.x);
					message.AddFloat(entity.Position.y);
					break;
				case "MainScript+Map+Entity+Flower":
					message.AddByte((byte) (1 + (entity as MainScript.Map.Entity.Flower).color));
					message.AddFloat(entity.Position.x);
					message.AddFloat(entity.Position.y);
					break;
				case "MainScript+Map+Entity+Stickman":
					message.AddByte(4);
					message.AddFloat(entity.Position.x);
					message.AddFloat(entity.Position.y);
					message.AddString((entity as MainScript.Map.Entity.Stickman).displayName);
					break;
				case "MainScript+Map+Entity+Airship":
					message.AddByte(5);
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

    private void OtherPlayerLeft(object sender, ClientDisconnectedEventArgs e) {}

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
			GetComponent<MenuScript>().BackToMainMenu();
	    } else {
			serverReconnectionAttempts++;
			JoinGame(connectedIp);
	    }
		GetComponent<MetaNetworkManager>().SendChatMessage(GetComponent<MetaNetworkManager>().localAccount.username + " has disconnected from server \"" + connectedServerName + " | Hosted by: " + connectedServerHostName + " (" + connectedServerVersion + ")" + "\".");
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

	[MessageHandler((ushort)MessageId.Join)]
	private static void ProcessPlayerJoin(Message message) {
		if (message.GetByte() == 0) {
			Camera.main.GetComponent<MainScript>().buildingPlacementBlueprint = Instantiate(Camera.main.GetComponent<MainScript>().Entities[5]);
			Camera.main.GetComponent<MainScript>().buildingPlacementMode = true;
			Camera.main.GetComponent<MainScript>().buildingPlacementType = 0;
			Camera.main.GetComponent<PlayerController>().Player = Camera.main.gameObject;
		} else {
			Camera.main.GetComponent<PlayerController>().Player = Camera.main.GetComponent<MainScript>().LoadedEntities[Camera.main.GetComponent<MetaNetworkManager>().localAccount.uuid];
		}
	}

	[MessageHandler((ushort)MessageId.Join)]
	private static void ProcessPlayerJoin(ushort sender, Message message) {
		string uuid = Camera.main.GetComponent<MetaNetworkManager>().connectedClients[sender].account.uuid;
		MainScript.Map.Entity.Stickman entity = new MainScript.Map.Entity.Stickman(new Vector2(25000, 25000), 100f, Camera.main.GetComponent<MetaNetworkManager>().connectedClients[sender].account.username, uuid, new AI.Null());
		Camera.main.GetComponent<MainScript>().serverMap.entities.Add(uuid, entity);
		Camera.main.GetComponent<NetworkManager>().playerEntities.Add(sender, uuid);
		Message message1 = Message.Create(MessageSendMode.Reliable, MessageId.SpawnEntity);
		message1.AddFloat(entity.Position.x);
		message1.AddFloat(entity.Position.y);
		Camera.main.GetComponent<NetworkManager>().Server.Send(message1, sender);
		Camera.main.GetComponent<NetworkManager>().Server.Send(Message.Create(MessageSendMode.Reliable, MessageId.Join).AddByte(1), sender);
	}

	[MessageHandler((ushort)MessageId.ChunkData)]
	private static void ProcessChunkData(Message message) {
	    int row = message.GetInt();
	    int column = message.GetInt();
	    byte data;
	    byte[,] chunk = new byte[25, 25];
		if (row == 0 && column == 0) {
			Camera.main.GetComponent<NetworkManager>().MinimapTexture = new Texture2D(1000, 1000);
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
	}

	[MessageHandler((ushort)MessageId.SpawnEntity)]
	private static void ProcessEntityData(Message message) {
		string uuid = message.GetString();
		byte type = message.GetByte();
		switch (type) {
			case 0:
				new MainScript.Map.Entity.Tree(new Vector2(message.GetFloat(), message.GetFloat()), uuid).Visualize();
				break;
			case 1:
			case 2:
			case 3:
				new MainScript.Map.Entity.Flower(new Vector2(message.GetFloat(), message.GetFloat()), (byte)(type-1), uuid).Visualize();
				break;
			case 4:
				new MainScript.Map.Entity.Stickman(new Vector2(message.GetFloat(), message.GetFloat()), message.GetString(), uuid).Visualize();
				break;
			case 5:
				new MainScript.Map.Entity.Airship(new Vector2(message.GetFloat(), message.GetFloat()), uuid).Visualize();
				break;
		}
	}

	[MessageHandler((ushort)MessageId.EntityMovement)]
	private static void ProcessEntityMovement(ushort sender, Message message) {
		//(Camera.main.GetComponent<MainScript>().serverMap.entities[Camera.main.GetComponent<NetworkManager>().playerEntities[sender]].ai as AI.Player).queuedActions.Enqueue(new AI.Player.PlayerAction(new Vector2(message.GetFloat(), message.GetFloat())));
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