using TMPro;
using UnityEngine.Tilemaps;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Riptide;
using UnityEngine;
using UnityEngine.UI;

public class MainScript : MonoBehaviour {
	[Serializable]
	public class Version {
		public enum SubversionType : byte {
			RELEASE = 0,
			BUILD,
			RELEASE_CANDIDATE
		}

		public byte major;
		public byte minor;
		public byte patch;
		public SubversionType subversionType;
		public byte subversion;

		public Version(byte newMajor, byte newMinor, byte newPatch, SubversionType newSubversionType, byte newSubversion) {
			major = newMajor;
			minor = newMinor;
			patch = newPatch;
			subversionType = newSubversionType;
			subversion = newSubversion;
		}

		public Version(Version newVersion) {
			major = newVersion.major;
			minor = newVersion.minor;
			patch = newVersion.patch;
			subversionType = newVersion.subversionType;
			subversion = newVersion.subversion;
		}

		public override string ToString() {
			switch (subversionType) {
				case SubversionType.RELEASE:
					return "v" + major + "." + minor + "." + patch;
				case SubversionType.BUILD:
					return "v" + major + "." + minor + "." + patch + "-build." + subversion;
				case SubversionType.RELEASE_CANDIDATE:
					return "v" + major + "." + minor + "." + patch + "-rc." + subversion;
			}
			return "invalid version";
		}

		public override bool Equals(object comp) {
			Version compare = comp as Version;
			if (compare == null)
				return false;
			return major == compare.major && minor == compare.minor && patch == compare.patch && subversionType == compare.subversionType && subversion == compare.subversion;
		}

		public override int GetHashCode() {
			return major + minor + patch + (byte) subversionType + subversion;
		}
	}

    public class Settings {
		public bool joinServerOnHost;

		public Settings() {
	    	joinServerOnHost = false;
		}
    }

    /*public class SaveFile {
		public Version version;

		public SaveFile() {
			version = new Version(Camera.main.GetComponent<MainScript>().currentVersion);
		}
    }*/

    public class Coordinate {
		public int x;
		public int y;

		public Coordinate(int newX, int newY) {
	    	x = newX;
	    	y = newY;
		}
    }

    [Serializable]
    public class Map {
		/*[Serializable]
		public class Tile {
	    	public byte type;
	    	public bool impassable;

	    	public Tile(byte newType, bool newImpassable = false) {
				type = newType;
				impassable = newImpassable;
	    	}
		}*/

		[Serializable]
		public abstract class Entity {
			[Serializable]
			public class Tree : Entity {
				public Tree(Vector2 newPosition, string newUUID) {
					position = new Vector3S(newPosition.x, newPosition.y, -0.1f);
					uuid = newUUID;
					ai = new AI.Null();
				}

				public override void Visualize() {
					visualization = Instantiate(Camera.main.GetComponent<MainScript>().Entities[GetEntityType("tree")], new Vector3(position.x, position.y, position.z), new Quaternion(0, 0, 0, 1));
					Camera.main.GetComponent<MainScript>().LoadedEntities.Add(uuid, visualization);
				}

				public override void ServerVisualize() {
					visualization = Instantiate(Camera.main.GetComponent<MainScript>().ServerEntities[GetEntityType("tree")], new Vector3(position.x-50000, position.y, position.z), new Quaternion(0, 0, 0, 1));
					Camera.main.GetComponent<MainScript>().ServerLoadedEntities.Add(uuid, visualization);
				}
			}

			[Serializable]
			public class Flower : Entity {
				public byte color;

				public Flower(Vector2 newPosition, byte newColor, string newUUID) {
					position = new Vector3S(newPosition.x, newPosition.y, 0.1f);
					color = newColor;
					uuid = newUUID;
					ai = new AI.Null();
				}

				public override void Visualize() {
					visualization = Instantiate(Camera.main.GetComponent<MainScript>().Entities[GetEntityType("flower")+color], new Vector3(position.x, position.y, position.z), new Quaternion(0, 0, 0, 1));
					Camera.main.GetComponent<MainScript>().LoadedEntities.Add(uuid, visualization);
				}

				public override void ServerVisualize() {}
			}

			[Serializable]
			public class Stickman : Entity, IHealth {
				public float Health {get; set;}
				public string displayName;

				public Stickman(Vector2 newPosition, string newDisplayName, string newUUID) {
					position = new Vector3S(newPosition.x, newPosition.y, 0);
					displayName = newDisplayName;
					uuid = newUUID;
					ai = new AI.Stickman();
					(ai as AI.Stickman).entity = this;
				}

				public Stickman(Vector2 newPosition, float newHealth, string newDisplayName, string newUUID) {
					position = new Vector3S(newPosition.x, newPosition.y, 0);
					Health = newHealth;
					displayName = newDisplayName;
					uuid = newUUID;
					ai = new AI.Stickman();
					(ai as AI.Stickman).entity = this;
				}

				public Stickman(Vector2 newPosition, float newHealth, string newDisplayName, string newUUID, AI newAi) {
					position = new Vector3S(newPosition.x, newPosition.y, 0);
					Health = newHealth;
					displayName = newDisplayName;
					ai = newAi;
					uuid = newUUID;
				}

				public override void Visualize() {
					visualization = Instantiate(Camera.main.GetComponent<MainScript>().Entities[GetEntityType("stickman")], new Vector3(position.x, position.y, position.z), new Quaternion(0, 0, 0, 1));
					Camera.main.GetComponent<MainScript>().LoadedEntities.Add(uuid, visualization);
					visualization.transform.GetChild(0).GetChild(0).GetComponent<TMP_Text>().text = displayName;
				}

				public override void ServerVisualize() {
					visualization = Instantiate(Camera.main.GetComponent<MainScript>().ServerEntities[GetEntityType("stickman")], new Vector3(position.x-50000, position.y, position.z), new Quaternion(0, 0, 0, 1));
					Camera.main.GetComponent<MainScript>().ServerLoadedEntities.Add(uuid, visualization);
				}
			}

			public interface IBuilding : IHealth {
				float Width {get; set;}
				float Height {get; set;}
			}

			public interface IHealth {
				float Health {get; set;}
			}

			[Serializable]
			public class Airship : Entity, IBuilding {
				public float Health {get; set;}
				public float Width {get; set;}
				public float Height {get; set;}

				public Airship(Vector2 newPosition, string newUUID) {
					position = new Vector3S(newPosition.x, newPosition.y, -0.3f);
					Width = 50;
					Height = 50;
					uuid = newUUID;
					ai = new AI.Null();
				}

				public override void Visualize() {
					visualization = Instantiate(Camera.main.GetComponent<MainScript>().Entities[GetEntityType("airship")], new Vector3(position.x, position.y, position.z), new Quaternion(0, 0, 0, 1));
					Camera.main.GetComponent<MainScript>().LoadedEntities.Add(uuid, visualization);
				}

				public override void ServerVisualize() {
					visualization = Instantiate(Camera.main.GetComponent<MainScript>().ServerEntities[GetEntityType("airship")], new Vector3(position.x-50000, position.y, position.z), new Quaternion(0, 0, 0, 1));
					Camera.main.GetComponent<MainScript>().ServerLoadedEntities.Add(uuid, visualization);
				}
			}

			public AI ai;
			private Vector3S position;
			public Vector3S Position {
				get {return position;}
				set {
					position = new Vector3(value.x, value.y, position.z);
					if (visualization != null) {
						if (visualization.GetComponent<SpriteRenderer>() == null) {
							visualization.transform.position = new Vector3(position.x - 50000, position.y, position.z);
						} else {
							visualization.transform.position = new Vector3(position.x, position.y, position.z);
						}
					}
				}
			}
			[NonSerialized] public GameObject visualization;
			public string uuid;

			public abstract void Visualize();
			public abstract void ServerVisualize();

			public static int GetEntityType(string name) {
				switch(name.ToLower()) {
					case "tree":
						return 0;
					case "flower":
						return 1;
					case "stickman":
						return 4;
					case "airship":
						return 5;
					default:
						return 255;
				}
			}
		}

		public string name;
		private byte[] tiles;
		public Dictionary<string, Entity> entities;

		public Map(string newName) {
			name = newName;
			tiles = new byte[1000000];
			entities = new();
		}

		public Map(string newName, byte[] newTiles) {
			name = newName;
			tiles = newTiles;
			entities = new();
		}

		public Map(string newName, byte[] newTiles, Dictionary<string, Entity> newEntities) {
			name = newName;
			tiles = newTiles;
			entities = newEntities;
		}

		public byte tileMap(int x, int y) {
			return tiles[x + (y * 1000)];
		}

		public void tileMap(int x, int y, byte tile) {
			tiles[x + (y * 1000)] = tile;
		}

		public byte[] tileMap() {
			return tiles;
		}

		public void LoadServerChunk(byte[,] chunk, int row, int column) {
			for (int x = 0; x < 25; x++) {
				for (int y = 0; y < 25; y++) {
					if (chunk[x, y] >= 64) {
						Camera.main.GetComponent<MainScript>().serverTilemap.SetTile(new Vector3Int(x+row*25, y+column*25, 0), Camera.main.GetComponent<MainScript>().tiles[0]);
					}
				}
			}
    	}
    }

	public Version currentVersion;
	public TMP_Text versionDisplay;
    [NonSerialized] public Map serverMap;
    public Settings settings;
    public Tilemap solidTilemap;
    public Tilemap nonSolidTilemap;
	public Tilemap serverTilemap;
    public UnityEngine.Tilemaps.Tile[] tiles;
    public GameObject[] Entities;
	public GameObject[] BuildingBlueprints;
	public GameObject[] ServerEntities;
	public Dictionary<string, GameObject> LoadedEntities;
	public Dictionary<string, GameObject> ServerLoadedEntities;
	public GameObject PlayerWaypoint;
	public GameObject AirshipWaypoint;
	public TMP_InputField SendMessageField;
    [Range(0f, 1f)]
    [SerializeField] public float oreDensity;
    [Range(0f, 1f)]
    [SerializeField] public float oreVariability;
    [Range(0f, 1f)]
    [SerializeField] public float treeDensity;
    [Range(0f, 1f)]
    [SerializeField] public float flowerDensity;
    float timer;
	public bool buildingPlacementMode;
	public GameObject buildingPlacementBlueprint;
	public byte buildingPlacementType;

    // Start is called before the first frame update
    void Start() {
		settings = new Settings();
		if (!Directory.Exists(Application.persistentDataPath + "/.maps"))
			Directory.CreateDirectory(Application.persistentDataPath + "/.maps");
		if (!File.Exists(Application.persistentDataPath + "/settings.json")) {
			File.WriteAllText(Application.persistentDataPath+"/settings.json",JsonUtility.ToJson(settings));
		} else {
			settings = JsonUtility.FromJson<Settings>(File.ReadAllText(Application.persistentDataPath + "/settings.json"));
		}
		LoadedEntities = new Dictionary<string, GameObject>();
		GetComponent<NetworkManager>().Start2();
		SendMessageField.onSubmit.AddListener(SendMessageToServer);
		versionDisplay.text = currentVersion.ToString();
    }

    // Update is called once per frame
    void Update() {
        timer -= Time.deltaTime;
		if (Camera.main.ScreenToWorldPoint(Input.mousePosition).x >= 0 && Camera.main.ScreenToWorldPoint(Input.mousePosition).y >= 0 && Camera.main.ScreenToWorldPoint(Input.mousePosition).x <= 50000 && Camera.main.ScreenToWorldPoint(Input.mousePosition).y <= 50000) {
			//Camera.main.ScreenToWorldPoint(Input.mousePosition).x;
		}
		foreach (GameObject entity in LoadedEntities.Values) {
			if (Vector3.Distance(transform.position, entity.transform.position) <= 1000) {
				entity.SetActive(true);
			} else {
				entity.SetActive(false);
			}
		}
		if (buildingPlacementMode) {
			if (buildingInLegalPosition()) {
				buildingPlacementBlueprint.GetComponent<SpriteRenderer>().color = Color.green;
			} else {
				buildingPlacementBlueprint.GetComponent<SpriteRenderer>().color = Color.red;
			}
			buildingPlacementBlueprint.transform.position = new Vector3(Camera.main.ScreenToWorldPoint(Input.mousePosition).x, Camera.main.ScreenToWorldPoint(Input.mousePosition).y, -1);
			if (Input.GetMouseButtonDown(0) && buildingInLegalPosition()) {
				GetComponent<NetworkManager>().Client.Send(Message.Create(MessageSendMode.Reliable, NetworkManager.MessageId.PlaceBuilding).AddByte(buildingPlacementType).AddFloat(buildingPlacementBlueprint.transform.position.x).AddFloat(buildingPlacementBlueprint.transform.position.y));
				buildingPlacementMode = false;
				Destroy(buildingPlacementBlueprint);
			}
		}
    }

	public void ServerMapFixedUpdate() {
		Map.Entity entity;
		foreach (KeyValuePair<string, Map.Entity> pair in serverMap.entities) {
			entity = pair.Value;
			entity.ai.getAICurrentAction().Execute(ref entity);
		}
	}

	public bool buildingInLegalPosition() {
		return !Physics2D.BoxCast(buildingPlacementBlueprint.transform.position, new Vector2(50, 50), 0, Vector2.zero) && buildingPlacementBlueprint.transform.position.x <= 50000 && buildingPlacementBlueprint.transform.position.y <= 50000 && buildingPlacementBlueprint.transform.position.x >= 0 && buildingPlacementBlueprint.transform.position.y >= 0;
	}

    public static void PrintMessage(string input) {
		TMP_Text message = GameObject.Find("Message").GetComponent<TMP_Text>();
		message.text = input;
		message.color = new Color32(0, 0, 0, 255);
    }

    public static void PrintMessageWarning(string input) {
		TMP_Text message = GameObject.Find("Message").GetComponent<TMP_Text>();
		message.text = input;
		message.color = new Color32(190, 183, 17, 255);
    }

    public static void PrintMessageError(string input) {
		TMP_Text message = GameObject.Find("Message").GetComponent<TMP_Text>();
		message.text = input;
		message.color = new Color32(190, 17, 17, 255);
    }

	//public void ChangeUsername() {
	//	GetComponent<MetaNetworkManager>().SendChatMessage(GetComponent<MetaNetworkManager>().localAccount.username + " has been renamed to " + UsernameField.text);
	//	GetComponent<MetaNetworkManager>().localAccount.username = UsernameField.text;
	//}

	public void SendMessageToServer(string message) {
		if (SendMessageField.wasCanceled)
        	return;
		GetComponent<MetaNetworkManager>().SendChatMessage(GetComponent<MetaNetworkManager>().localAccount.username + ": " + message);
		SendMessageField.text = "";
	}

    public void LoadChunk(byte[,] chunk, int row, int column) {
		for (int x = 0; x < 25; x++) {
			for (int y = 0; y < 25; y++) {
				if (chunk[x, y] >= 64) {
					solidTilemap.SetTile(new Vector3Int(x+row*25, y+column*25, 0), tiles[chunk[x, y] - 64]);
					if (IsStone(chunk[x, y])) {
						nonSolidTilemap.SetTile(new Vector3Int(x+row*25, y+column*25, 0), tiles[1]);
					}
				} else {
					nonSolidTilemap.SetTile(new Vector3Int(x+row*25, y+column*25, 0), tiles[chunk[x, y]]);
				}
			}
		}
    }

    public void setSettingJoinServerOnHost(bool setting) {
		settings.joinServerOnHost = setting;
		File.WriteAllText(Application.persistentDataPath+"/settings.json",JsonUtility.ToJson(settings));
    }

    private FastNoiseLite GenerateNoiseMap(int seed, int octaves, float frequency) {
		FastNoiseLite heightMap = new FastNoiseLite(seed);
		heightMap.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
		heightMap.SetFractalType(FastNoiseLite.FractalType.FBm);
		heightMap.SetFractalOctaves(octaves);
		heightMap.SetFrequency(frequency);
    	return heightMap;
    }

    public Map GenerateMap(string name, int seed) {
		FastNoiseLite mainNoiseMap = GenerateNoiseMap(seed, 6, 0.003f);
		float[,] falloffMap = FalloffGenerator.GenerateFalloffMap(1000);
		Map map = new Map(name);
		for (int x = 0; x < 1000; x++) {
				for (int y = 0; y < 1000; y++) {
			float noise = mainNoiseMap.GetNoise(x, y) - falloffMap[x, y];
			if (noise <= -0.4) {
				map.tileMap(x ,y, 80);
			} else if (noise <= -0.2) {
				map.tileMap(x ,y, 79);
			} else if (noise <= -0.1) {
				map.tileMap(x ,y, 78);
			} else if (noise <= -0.05) {
				map.tileMap(x ,y, 17);
			} else if (noise <= 0) {
				map.tileMap(x ,y, 8);
			} else if (noise <= 0.25) {
				map.tileMap(x ,y, GetRandomGrassType());
			} else {
				map.tileMap(x ,y, 64);
			}
				}
			}
		FastNoiseLite oreNoiseMap = GenerateNoiseMap(seed+1, 1, 0.003f);
		for (int x = 0; x < 1000; x++) {
			for (int y = 0; y < 1000; y++) {
				if (IsStone(map.tileMap(x, y))) {
					if (UnityEngine.Random.Range(0f, 1f) <= oreDensity * (1 + (oreNoiseMap.GetNoise(x, y) * oreVariability))) {
						float oreType = UnityEngine.Random.Range(0f, 1f);
						if (oreType <= 0.3f) {
							map.tileMap(x, y, 5);
						} else if (oreType <= 0.5f) {
							map.tileMap(x, y, 4);
						} else if (oreType <= 0.65f) {
							map.tileMap(x, y, 2);
						} else if (oreType <= 0.8f) {
							map.tileMap(x, y, 6);
						} else if (oreType <= 0.9f) {
							map.tileMap(x, y, 7);
						} else {
							map.tileMap(x, y, 3);
						}
					}
				}
			}
		}
		FastNoiseLite biomeMap = GenerateNoiseMap(seed+2, 1, 0.003f);
		for (int x = 0; x < 1000; x++) {
			for (int y = 0; y < 1000; y++) {
				string uuid = System.Guid.NewGuid().ToString();
				if (biomeMap.GetNoise(x, y) <= 0) {
					if (IsGrass(map.tileMap(x, y))) {
						if (UnityEngine.Random.Range(0f, 1f) <= treeDensity / 5f) {
							map.entities.Add(uuid, new Map.Entity.Tree(new Vector2(x*50+UnityEngine.Random.Range(-0.5f, 0.5f), y*50+UnityEngine.Random.Range(-0.5f, 0.5f)), uuid));
						} else if (UnityEngine.Random.Range(0f, 1f) <= flowerDensity) {
							map.entities.Add(uuid, new Map.Entity.Flower(new Vector2(x*50+UnityEngine.Random.Range(-0.5f, 0.5f), y*50+UnityEngine.Random.Range(-0.5f, 0.5f)), (byte) UnityEngine.Random.Range(0,3), uuid));
						}
					}
				} else {
					if (IsGrass(map.tileMap(x, y))) {
						if (UnityEngine.Random.Range(0f, 1f) <= treeDensity) {
							map.entities.Add(uuid, new Map.Entity.Tree(new Vector2(x*50+UnityEngine.Random.Range(-0.5f, 0.5f), y*50+UnityEngine.Random.Range(-0.5f, 0.5f)), uuid));
						} else if (UnityEngine.Random.Range(0f, 1f) <= flowerDensity / 5f) {
							map.entities.Add(uuid, new Map.Entity.Flower(new Vector2(x*50+UnityEngine.Random.Range(-0.5f, 0.5f), y*50+UnityEngine.Random.Range(-0.5f, 0.5f)), (byte) UnityEngine.Random.Range(0,3), uuid));
						}
					}
				}
			}
		}
		return map;
    }

    public static byte GetRandomGrassType() {
		return (byte) UnityEngine.Random.Range(9,14);
    }

    public static bool IsStone(byte type) {
		return type <= 7 || (type >= 64 && type <= 72);
    }

    public static bool IsGrass(byte type) {
		return type >= 9 && type <= 13;
    }
}