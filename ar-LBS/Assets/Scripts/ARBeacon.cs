using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR.ARFoundation;


public enum BroadcastMode {
	send	= 0,
	receive	= 1,
	unknown = 2
}
public enum BroadcastState {
	inactive = 0,
	active	 = 1
}

internal class ARBeacon : MonoBehaviour {
	public float minimumDistance = .1f;
	public float maximumDistance = .2f;
	public float destoryTime = 0.0f;
	public GameObject arCamera;
	[SerializeField]
	private ARPlaneManager arPlaneManager;
	[SerializeField]
	private ARSession arSession;
	/// <summary>
	/// The prefab to instantiate when Beacon state is immediate.
	/// </summary>
	[SerializeField]
	[Tooltip("Instantiates this prefab when the Beacon state is immediate.")]
	GameObject m_PlacedPrefab1;

	[SerializeField]
	[Tooltip("Instantiates this prefab when the Beacon state is immediate.")]
	GameObject m_PlacedPrefab2;

	/// <summary>
	/// The prefab to instantiate on touch.
	/// </summary>
	public GameObject placedPrefab1
	{
		get { return m_PlacedPrefab1; }
		set { m_PlacedPrefab1 = value; }
	}

	public GameObject placedPrefab2
	{
		get { return m_PlacedPrefab2; }
		set { m_PlacedPrefab2 = value; }
	}

	/// <summary>
	/// The object instantiated as a result of a successful Beacon state.
	/// </summary>
	public GameObject spawnedObject1 { get; private set; }
	public GameObject spawnedObject2 { get; private set; }

	[SerializeField]
	private GameObject welcomePanel;
	[SerializeField]
	private Button dismissButton;
	[SerializeField]
	private Button homeButton;
	[SerializeField]
	private Text txt_FoundBeacon_infoText;



	[SerializeField]
	private GameObject _menuScreen;

	/*** Beacon Properties ***/
	private string s_Region;
	private string s_UUID;
	private string s_Major;
	private string s_Minor;

	/** Input **/
	// beacontype
	private BeaconType bt_Type = BeaconType.iBeacon;

	private BroadcastMode bm_Mode;

	// Beacon BroadcastState (Start, Stop)
	[SerializeField]
	private Image img_ButtonBroadcastState;
	[SerializeField]
	private Text txt_BroadcastState_ButtonText;
	[SerializeField]
	private BroadcastState bs_State;

	// GameObject for found Beacons
	[SerializeField]
	private GameObject go_ScrollViewContent;

	[SerializeField]
	private GameObject go_FoundBeacon;
	List<GameObject> go_FoundBeaconCloneList = new List<GameObject>();
	GameObject go_FoundBeaconClone;
	private float f_ScrollViewContentRectWidth;
	private float f_ScrollViewContentRectHeight;
	private int i_BeaconCounter = 0;

	// Receive
	private List<Beacon> mybeacons = new List<Beacon>();

    private void Awake()
    {
		dismissButton.onClick.AddListener(Dismiss);
    }

    private void Start() {
		setBeaconPropertiesAtStart(); // please keep here!
		arPlaneManager.enabled = false;
		BluetoothState.EnableBluetooth();
		
		f_ScrollViewContentRectWidth = ((RectTransform)go_FoundBeacon.transform).rect.width;
		f_ScrollViewContentRectHeight = ((RectTransform)go_FoundBeacon.transform).rect.height;
		BluetoothState.Init();
	}

	private void Dismiss() => welcomePanel.SetActive(false);
	private void Home() => welcomePanel.SetActive(true);

    private void Update()
    {
		if (welcomePanel.activeSelf)
			return;
		homeButton.onClick.AddListener(Home);
    }

    private void setBeaconPropertiesAtStart() {
		RestorePlayerPrefs();
		if (bm_Mode == BroadcastMode.unknown) { // first start
			bm_Mode = BroadcastMode.receive;
			bt_Type = BeaconType.iBeacon;
			if (iBeaconReceiver.regions.Length != 0) {
				Debug.Log("check iBeaconReceiver-inspector");
				s_Region	= iBeaconReceiver.regions[0].regionName;
				bt_Type 	= iBeaconReceiver.regions[0].beacon.type;
				if (bt_Type == BeaconType.iBeacon) {
					s_UUID = iBeaconReceiver.regions[0].beacon.UUID;
					s_Major = iBeaconReceiver.regions[0].beacon.major.ToString();
					s_Minor = iBeaconReceiver.regions[0].beacon.minor.ToString();
				} 
			}
		}
		bs_State = BroadcastState.inactive;
		//SetBroadcastMode();
		SetBroadcastState();
		Debug.Log("Beacon properties and modes restored");
	}

	// BroadcastState
	public void btn_StartStop()
	{
		//Debug.Log("Button Start / Stop pressed");
		/*** Beacon will start ***/
		if (bs_State == BroadcastState.inactive)
		{
			// ReceiveMode
			if (bm_Mode == BroadcastMode.receive)
			{
				iBeaconReceiver.BeaconRangeChangedEvent += OnBeaconRangeChanged;
				iBeaconReceiver.Scan();
				Debug.Log("Listening for beacons");
			}
			// SendMode
			else
			{
				iBeaconServer.Transmit();
				Debug.Log("It is on, go sending");
			}
			bs_State = BroadcastState.active;
			txt_FoundBeacon_infoText.text = "Searching for Beacons";
		}
		else
		{
			if (bm_Mode == BroadcastMode.receive)
			{// Stop for receive
				iBeaconReceiver.Stop();
				iBeaconReceiver.BeaconRangeChangedEvent -= OnBeaconRangeChanged;
				removeFoundBeacons();
				if(spawnedObject1 != null)Destroy(spawnedObject1, destoryTime);
				if(spawnedObject2 !=null)Destroy(spawnedObject2, destoryTime);
				txt_FoundBeacon_infoText.text = "Waiting to Start";
			}
			else
			{ // Stop for send
				iBeaconServer.StopTransmit();
			}
			bs_State = BroadcastState.inactive;
		}
		SetBroadcastState();
		SavePlayerPrefs();
	}
	private void SetBroadcastState() {
		// setText
		if (bs_State == BroadcastState.inactive)
			txt_BroadcastState_ButtonText.text = "Start";
		else
			txt_BroadcastState_ButtonText.text = "Stop";
	}

	private void OnBeaconRangeChanged(Beacon[] beacons) {
		foreach (Beacon b in beacons) {
			//Instantiate In the Camera Postion
			if (spawnedObject1 == null)
			{
				if (b.regionName.ToString() == "com.yuu.cube")
				{
					if (b.accuracy < minimumDistance)
					{
						spawnedObject1 = Instantiate(m_PlacedPrefab1, arCamera.transform.position, Quaternion.identity);
						txt_FoundBeacon_infoText.text = "Found Beacon";
					}
				}
			}
			else
			{
				if (b.regionName.ToString() == "com.yuu.cube")
				{
					if (b.accuracy > maximumDistance)
					{
						Destroy(spawnedObject1, destoryTime);
						txt_FoundBeacon_infoText.text = "Searching for Beacons";
					}
				}
			}
				//Auto Placement
				if (spawnedObject2 == null)
				{
					if (b.regionName.ToString() == "com.yuu.sphere")
					{
						if (b.accuracy < minimumDistance)
						{
							arPlaneManager.enabled = true;
						txt_FoundBeacon_infoText.text = "Found Beacon";

						foreach (ARPlane plane in arPlaneManager.trackables)
							{
								plane.gameObject.SetActive(arPlaneManager.enabled);
								spawnedObject2 = Instantiate(m_PlacedPrefab2, plane.transform.position, Quaternion.identity);
							}

						}

					}
				}
				else
				{
				foreach (ARPlane plane in arPlaneManager.trackables)
				{
					plane.gameObject.SetActive(arPlaneManager.enabled);
					spawnedObject2.transform.position = plane.transform.position;
				}
				if (b.regionName.ToString() == "com.yuu.sphere")
					{
						if (b.accuracy > maximumDistance)
						{
							arPlaneManager.enabled = false;
						txt_FoundBeacon_infoText.text = "Searching for Beacons";
						foreach (ARPlane plane in arPlaneManager.trackables)
							{
								plane.gameObject.SetActive(arPlaneManager.enabled);
								Destroy(spawnedObject2, destoryTime);
							}
							arSession.Reset();

						}
					}
				}
					


			var index = mybeacons.IndexOf(b);
			if (index == -1) {
				mybeacons.Add(b);
			} else {
				mybeacons[index] = b;
			}
		}
		for (int i = mybeacons.Count - 1; i >= 0; --i) {
			if (mybeacons[i].lastSeen.AddSeconds(10) < DateTime.Now) {
				mybeacons.RemoveAt(i);
			}
		}
		DisplayOnBeaconFound();
	}

    private void DisplayOnBeaconFound() {
		removeFoundBeacons();
		RectTransform rt_Content = (RectTransform)go_ScrollViewContent.transform;
		foreach (Beacon b in mybeacons) {
			// create clone of foundBeacons
			go_FoundBeaconClone = Instantiate(go_FoundBeacon);
			// make it child of the ScrollView
			go_FoundBeaconClone.transform.SetParent(go_ScrollViewContent.transform);
			// get resolution based scalefactor
			float f_scaler = ((RectTransform)go_FoundBeaconClone.transform).localScale.y;
			Vector2 v2_scale = new Vector2(1,1);
			// reset scalefactor
			go_FoundBeaconClone.transform.localScale = v2_scale;
			// get anchorposition
			Vector3 pos = go_ScrollViewContent.transform.position; 
			// positioning
			pos.y -= f_ScrollViewContentRectHeight/f_scaler * i_BeaconCounter;
			go_FoundBeaconClone.transform.position = pos;
			i_BeaconCounter++;
			// resize scrollviewcontent
			rt_Content.sizeDelta = new Vector2(f_ScrollViewContentRectWidth,f_ScrollViewContentRectHeight*i_BeaconCounter);
			go_FoundBeaconClone.SetActive(true);
			// adding reference to instance
			go_FoundBeaconCloneList.Add(go_FoundBeaconClone);
			// get textcomponents
			Text[]Texts	= go_FoundBeaconClone.GetComponentsInChildren<Text>();
			// deleting placeholder
			foreach (Text t in Texts)
				t.text = "";
			Debug.Log ("fond Beacon: " + b.ToString());
			switch (b.type) {
			case BeaconType.iBeacon:
				Texts[0].text 	= "UUID:";
				Texts[1].text 	= b.UUID.ToString();
				Texts[2].text 	= "Major:";
				Texts[3].text	= b.major.ToString();
				Texts[4].text 	= "Minor:";
				Texts[5].text	= b.minor.ToString();
				Texts[6].text 	= "Range:";
				Texts[7].text	= b.range.ToString();
				Texts[8].text 	= "Strength:";
				Texts[9].text	= b.strength.ToString() + " db";
				Texts[10].text 	= "Accuracy:";
				Texts[11].text	= b.accuracy.ToString().Substring(0,10) + " m";
				Texts[12].text 	= "Rssi:";
				Texts[13].text	= b.rssi.ToString() + " db";
				break;
			case BeaconType.EddystoneUID:
				Texts[0].text 	= "Namespace:";
				Texts[1].text 	= b.UUID;
				Texts[2].text 	= "Instance:";
				Texts[3].text	= b.instance;
				Texts[6].text 	= "Range:";
				Texts[7].text	= b.range.ToString();
				break;
			case BeaconType.EddystoneURL:
				Texts[0].text 	= "URL:";
				Texts[1].text 	= b.UUID.ToString();
				Texts[2].text 	= "Range:";
				Texts[3].text	= b.range.ToString();
				break;
			case BeaconType.EddystoneEID:
				Texts[0].text 	= "EID:";
				Texts[1].text 	= b.UUID.ToString();
				Texts[2].text 	= "Range:";
				Texts[3].text	= b.range.ToString();
				break;
			default:
				break;
			}
		}
	}

	private void removeFoundBeacons() {
		Debug.Log("removing all found Beacons");
		// set scrollviewcontent to standardsize
		RectTransform rt_Content = (RectTransform)go_ScrollViewContent.transform;
		rt_Content.sizeDelta = new Vector2(f_ScrollViewContentRectWidth,f_ScrollViewContentRectHeight);
		// destroying each clone
		foreach (GameObject go in go_FoundBeaconCloneList)
			Destroy(go);
		go_FoundBeaconCloneList.Clear();
		i_BeaconCounter = 0;
	}

	// PlayerPrefs
	private void SavePlayerPrefs() {
		PlayerPrefs.SetInt("Type", (int)bt_Type);
		PlayerPrefs.SetString("Region", s_Region);
		PlayerPrefs.SetString("UUID", s_UUID);
		PlayerPrefs.SetString("Major", s_Major);
		PlayerPrefs.SetString("Minor", s_Minor);
		PlayerPrefs.SetInt("BroadcastMode", (int)bm_Mode);
		//PlayerPrefs.DeleteAll();
	}
	private void RestorePlayerPrefs() {
		if (PlayerPrefs.HasKey("Type"))
			bt_Type = (BeaconType)PlayerPrefs.GetInt("Type");
		if (PlayerPrefs.HasKey("Region"))
			s_Region = PlayerPrefs.GetString("Region");
		if (PlayerPrefs.HasKey("UUID"))
			s_UUID = PlayerPrefs.GetString("UUID");
		if (PlayerPrefs.HasKey("Major"))
			s_Major = PlayerPrefs.GetString("Major");
		if (PlayerPrefs.HasKey("Minor"))
			s_Minor = PlayerPrefs.GetString("Minor");
		if (PlayerPrefs.HasKey("BroadcastMode"))
			bm_Mode = (BroadcastMode)PlayerPrefs.GetInt("BroadcastMode");
		else 
			bm_Mode = BroadcastMode.unknown;
	}
}