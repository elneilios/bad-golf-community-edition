//////////////////////////////////////////////////////////////////////////////////////////////////////
//IMPORTANT NOTE:
//this script must be attached to an object created once in a player's instance, i.e. networkObject
//////////////////////////////////////////////////////////////////////////////////////////////////////

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BallMarker : MonoBehaviour {
    public GameObject m_myBallMarkerPrefab;
    public GameObject m_enemyBallMarkerPrefab;

    private networkVariables m_nvs;
    private PlayerInfo m_myPlayerInfo;
    private GameObject m_myBall;
    private GameObject m_myBallMarker;
    private Dictionary<PlayerInfo, GameObject> m_enemyBallMarkers;
    private Camera m_myCamera;

    private bool m_initialized = false;
    private bool m_moveUp = false;
    private float m_positionOffset = 0.0f;
    private int m_numPlayersExpected;
    private int m_myLastMode = 0;

    private const float k_maxBallScalar = 1.2f;
    private const float k_heightOffsetFromBall = 3.0f;
    private const float k_maxAlphaPercentEnemyMarkers = 0.35f;


	void Start () 
    {
        AttemptInitialize();
	}
	
	void Update () 
    {
        //never do anything if network variables weren't found
        if (!m_initialized) {
            AttemptInitialize();
            return;
        }

        //check what mode player is in - swinging or driving?
        CheckMode();

        //first, make sure the players we expect are there, or clean up
        //appropriate containing structures
        CheckPlayerListValidity();
        //then, update marker positions
        UpdatePositions();
        
	}

    //hide markers (layer 12) if swinging
    void CheckMode()
    {
        int currMode = m_myPlayerInfo.currentMode;
        if (currMode != m_myLastMode) {
            if (currMode == 1) {
                m_myCamera.cullingMask &= ~(1 << 12);
            } else {
                m_myCamera.cullingMask |= (1 << 12);
            }
            m_myLastMode = currMode;
        }
    }

    void CheckPlayerListValidity()
    {
        // -1 is to account for player not being in enemy marker list
        if (m_numPlayersExpected < m_nvs.players.Count - 1) {
            RegisterNewPlayers();
        } else if (m_numPlayersExpected > m_nvs.players.Count - 1) {
            CleanupPlayerList();
        }
    }

    void CleanupPlayerList()
	{
		// have to treat dictionaries weirdly
		ArrayList keysToRemove = new ArrayList();	// create a blank ArrayList
        foreach (KeyValuePair<PlayerInfo,GameObject> keypair in m_enemyBallMarkers) {	// iterate through the dictionary
			if (!m_nvs.players.Contains(keypair.Key)) {	// check if we have a copy
				keysToRemove.Add(keypair.Key);
            }
		}
		foreach(PlayerInfo keyToRemove in keysToRemove) {
			if (m_enemyBallMarkers.ContainsKey(keyToRemove)) {	// if something does need removing then remove it
				Destroy(m_enemyBallMarkers[keyToRemove]);
				m_enemyBallMarkers.Remove(keyToRemove);
				m_numPlayersExpected--;
			}
		}
    }

    void RegisterNewPlayers()
    {
        foreach (PlayerInfo player in m_nvs.players) {
            if (!m_enemyBallMarkers.ContainsKey(player)) {
                if (player != m_myPlayerInfo) {
                    GameObject playerBall = player.ballGameObject;
                    Vector3 thisBallMarkerPos = playerBall.transform.position;
                    thisBallMarkerPos.y += k_heightOffsetFromBall;
                    GameObject thisBallMarker = GameObject.Instantiate(m_enemyBallMarkerPrefab) as GameObject;
                    thisBallMarker.transform.position = thisBallMarkerPos;

                    m_enemyBallMarkers.Add(player, thisBallMarker);
                    m_numPlayersExpected++;
                }
            }
        }
    }

    void UpdatePositions()
    {
        Vector3 ballPos = m_myBall.transform.position;
        Vector3 startingPos = new Vector3(ballPos.x, ballPos.y, ballPos.z);
        startingPos.y += (k_heightOffsetFromBall + m_positionOffset); //needs to be high enough to prevent weird collision issues with ball

        m_myBallMarker.transform.position = startingPos;

        m_myBallMarker.transform.rotation = m_myCamera.transform.rotation; //billboard ball marker towards the camera

        UpdateColorScaleToDistance(m_myBallMarker, true);

        foreach (PlayerInfo player in m_nvs.players) {
            if (m_enemyBallMarkers.ContainsKey(player)) {
                if (player != m_myPlayerInfo) {
                    GameObject playerBall = m_enemyBallMarkers[player];
                    Vector3 thisBallMarkerPos = player.ballGameObject.transform.position;
                    thisBallMarkerPos.y += k_heightOffsetFromBall;
                    playerBall.transform.position = thisBallMarkerPos;

                    playerBall.transform.rotation = m_myCamera.transform.rotation; //billboard ball marker towards the camera
                    UpdateColorScaleToDistance(playerBall, false);
                }
            }
        }
    }

    void UpdateColorScaleToDistance(GameObject objectToUpdate, bool myBall)
    {
        Renderer objRenderer = objectToUpdate.GetComponentInChildren<Renderer>();

        //if renderer is not obtained, bail out
        if (objRenderer == null) return;
        Color objColor = objRenderer.material.GetColor("_Color");
        float distance = Vector3.Distance(objectToUpdate.transform.position, m_myPlayerInfo.cartGameObject.transform.position);

        if (myBall) {
            objColor.a = Mathf.Abs(distance * 0.25f / 10.0f);
        } else {
            objColor.a = Mathf.Min(k_maxAlphaPercentEnemyMarkers, Mathf.Abs(distance * 0.25f / 10.0f));
        }

        Vector3 scale = objectToUpdate.transform.localScale;
        scale.x = Mathf.Max(distance / 15.0f * k_maxBallScalar, k_maxBallScalar);
        scale.y = Mathf.Max(distance / 15.0f * k_maxBallScalar, k_maxBallScalar);

        objectToUpdate.transform.localScale = scale;

        objRenderer.material.SetColor("_Color", objColor);
    }

    void AttemptInitialize()
    {
        m_nvs = FindObjectOfType<networkVariables>() as networkVariables;

        //confirm ability to get network variables, else return here without setting initialization flag
        if (m_nvs == null) {
            //Debug.Log("Unable to find network variables!");
            return;
        }



        Initialize();
    }

    void Initialize()
    {
        m_myPlayerInfo = m_nvs.myInfo;

        //can't do anything else if we don't have PlayerInfo resources loaded!
        if (m_myPlayerInfo.cartGameObject == null) return;

        //m_myCamera = m_myPlayerInfo.cartGameObject.transform.FindChild("multi_buggy_cam").gameObject.camera;
        m_myCamera = Camera.main;

        m_myBall = m_myPlayerInfo.ballGameObject;

        //need own ball and camera to be existent to initialize
        if (m_myBall == null || m_myCamera == null) {
            return;
        }

        m_myBallMarker = GameObject.Instantiate(m_myBallMarkerPrefab) as GameObject;

        Vector3 startingPos = m_myBall.transform.position;
        startingPos.y += 2.5f; //needs to be high enough to prevent weird collision issues with ball

        m_myBallMarker.transform.position = startingPos;

        StartCoroutine(MoveObject(0.0f, 1.0f, 0.5f));

        //initialize enemy ball markers
        m_enemyBallMarkers = new Dictionary<PlayerInfo, GameObject>();
        foreach (PlayerInfo player in m_nvs.players) {
            // do NOT want to duplicate own ball marker
            if (player != m_myPlayerInfo) {
                GameObject playerBall = player.ballGameObject;
                Vector3 thisBallMarkerPos = playerBall.transform.position;
                thisBallMarkerPos.y += 2.5f;
                GameObject thisBallMarker = GameObject.Instantiate(m_enemyBallMarkerPrefab) as GameObject;
                thisBallMarker.transform.position = thisBallMarkerPos;

                m_enemyBallMarkers.Add(player, thisBallMarker);
            }
        }

        //set how many players were connected at initialization
        m_numPlayersExpected = m_nvs.players.Count - 1;
        m_initialized = true;
    }

    IEnumerator MoveObject(float min, float max, float overTime)
    {
        m_moveUp = !m_moveUp;
        float startTime = Time.time;

        while (Time.time < startTime + overTime) {
            if (m_moveUp) {
                m_positionOffset = Mathf.Lerp(min, max, (Time.time - startTime) / overTime);
            } else {
                m_positionOffset = Mathf.Lerp(max, min, (Time.time - startTime) / overTime);
            }
            yield return null;
        }

        //finalize position (Lerp will leave it slightly off)
        if (m_moveUp) {
            m_positionOffset = max;
        } else {
            m_positionOffset = min;
        }
        
        StartCoroutine(MoveObject(0.0f, 1.0f, 0.5f));
    }
}
