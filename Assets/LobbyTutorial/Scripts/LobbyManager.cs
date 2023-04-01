using System.Collections;
using System.Collections.Generic;
using Unity.Services.Core;
using Unity.Services.Authentication;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

public class LobbyManager : MonoBehaviour
{

    [Header("Main Menu")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private Button getLobbiesListBtn;
    [SerializeField] private GameObject lobbyInfoPrefab;
    [SerializeField] private GameObject lobbiesInfoContent;
    [SerializeField] private TMP_InputField playerNameIF;

    [Space(10)]
    [Header("Create Room Panel")]
    [SerializeField] private GameObject createRoomPanel;
    [SerializeField] private TMP_InputField roomNameIF;
    [SerializeField] private TMP_InputField maxPlayersIF;
    [SerializeField] private Button createRoomBtn;
    [SerializeField] private Toggle isPrivateToggle;


    [Space(10)]
    [Header("Room Panel")]
    [SerializeField] private GameObject roomPanel;
    [SerializeField] private TextMeshProUGUI roomName;
    [SerializeField] private TextMeshProUGUI roomCode;
    [SerializeField] private GameObject playerInfoContent;
    [SerializeField] private GameObject playerInfoPrefab;
    [SerializeField] private Button leaveRoomButton;
    [SerializeField] private Button startGameButton;



    [Space(10)]
    [Header("Join Room With Code")]
    [SerializeField] private GameObject joinRoomPanel;
    [SerializeField] private TMP_InputField roomCodeIF;
    [SerializeField] private Button joinRoomBtn;

    [Space(10)]
    [Header("Game Panel")]
    [SerializeField] private GameObject gamePanel;



    private Lobby currentLobby;


    private string playerId;
    // Start is called before the first frame update
    async void Start()
    {
        await UnityServices.InitializeAsync();
        AuthenticationService.Instance.SignedIn += () =>
        {
            playerId = AuthenticationService.Instance.PlayerId;
            Debug.Log("Signed in " + playerId);
        };
        await AuthenticationService.Instance.SignInAnonymouslyAsync();


        createRoomBtn.onClick.AddListener(CreateLobby);
        joinRoomBtn.onClick.AddListener(JoinLobbyWithCode);
        getLobbiesListBtn.onClick.AddListener(ListPublicLobbies);

        playerNameIF.onValueChanged.AddListener(delegate
        {
            PlayerPrefs.SetString("Name", playerNameIF.text);
        });

        playerNameIF.text = PlayerPrefs.GetString("Name");

        leaveRoomButton.onClick.AddListener(LeaveRoom);

    }

    // Update is called once per frame
    void Update()
    {
        HandleLobbiesListUpdate();
        HandleLobbyHeartbeat();
        HandleRoomUpdate();
    }


    private async void CreateLobby()
    {
        try
        {
            string lobbyName = roomNameIF.text;
            int.TryParse(maxPlayersIF.text, out int maxPlayers);
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = isPrivateToggle.isOn,
                Player = GetPlayer(),
                Data=new Dictionary<string, DataObject>
                {
                    {"IsGameStarted", new DataObject(DataObject.VisibilityOptions.Member,"false") }
                }
            };
            currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
            EnterRoom();
        }
        catch(LobbyServiceException e)
        {
            Debug.Log(e);
        }
        
    }




   

    private void EnterRoom()
    {
        mainMenuPanel.SetActive(false);
        createRoomPanel.SetActive(false);
        joinRoomPanel.SetActive(false);
        roomPanel.SetActive(true);
        roomName.text = currentLobby.Name;
        roomCode.text = currentLobby.LobbyCode;
        VisualizeRoomDetails();
    }

    private float roomUpdateTimer = 2f;
    private async void HandleRoomUpdate()
    {
        if (currentLobby != null)
        {
            roomUpdateTimer -= Time.deltaTime;
            if (roomUpdateTimer <= 0)
            {
                roomUpdateTimer = 2f;
                try
                {
                    if (IsinLobby())
                    {
                        currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
                        VisualizeRoomDetails();
                    }
                        
                }
                catch (LobbyServiceException e)
                {
                    Debug.Log(e);
                    if((e.Reason == LobbyExceptionReason.Forbidden || e.Reason==LobbyExceptionReason.LobbyNotFound))
                    {
                        currentLobby = null;
                        ExitRoom();
                    }
                }
            }
        }
        
    }


    


    private bool IsinLobby()
    {
        foreach(Player _player in currentLobby.Players)
        {
            if (_player.Id == playerId)
            {
                return true;
            }
        }
        currentLobby = null;
        return false;
    }
    


    private void VisualizeRoomDetails()
    {
        for(int i = 0; i < playerInfoContent.transform.childCount; i++)
        {
            Destroy(playerInfoContent.transform.GetChild(i).gameObject);
        }
        if (IsinLobby())
        {
            foreach (Player player in currentLobby.Players)
            {
                GameObject newPlayerInfo = Instantiate(playerInfoPrefab, playerInfoContent.transform);
                newPlayerInfo.GetComponentInChildren<TextMeshProUGUI>().text = player.Data["PlayerName"].Value;
                if (IsHost() && player.Id!=playerId)
                {
                    Button kickBtn = newPlayerInfo.GetComponentInChildren<Button>(true);
                    kickBtn.onClick.AddListener(() => KickPlayer(player.Id));
                    kickBtn.gameObject.SetActive(true);

                }
            }

            if (IsHost())
            {
                startGameButton.onClick.AddListener(StartGame);
                startGameButton.GetComponentInChildren<TextMeshProUGUI>().text = "Start Game";
                startGameButton.gameObject.SetActive(true);
            }
            else
            {
                if (IsGameStarted())
                {
                    startGameButton.onClick.AddListener(EnterGame);
                    startGameButton.gameObject.SetActive(true);
                    startGameButton.GetComponentInChildren<TextMeshProUGUI>().text = "Enter Game";
                }
                else
                {
                    startGameButton.onClick.RemoveAllListeners();
                    startGameButton.gameObject.SetActive(false);
                }
            }

        }
        else
        {
            ExitRoom();
        }
        
    }


    


    private async void ListPublicLobbies()
    {
        try
        {
            QueryResponse response =  await LobbyService.Instance.QueryLobbiesAsync();
            VisualizeLobbyList(response.Results);
        }
        catch(LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }


    private float updateLobbiesListTimer = 2f;

    private void HandleLobbiesListUpdate()
    {
        updateLobbiesListTimer -= Time.deltaTime;
        if (updateLobbiesListTimer <= 0)
        {
            ListPublicLobbies();
            updateLobbiesListTimer = 2f;
        }
    }


    private void VisualizeLobbyList(List<Lobby> _publicLobbies)
    {
        // We need to clear previous info
        for(int i = 0; i < lobbiesInfoContent.transform.childCount; i++)
        {
            Destroy(lobbiesInfoContent.transform.GetChild(i).gameObject);
        }
        foreach(Lobby _lobby in _publicLobbies)
        {
            GameObject newLobbyInfo =  Instantiate(lobbyInfoPrefab,lobbiesInfoContent.transform);
            var lobbyDetailsTexts =  newLobbyInfo.GetComponentsInChildren<TextMeshProUGUI>();
            lobbyDetailsTexts[0].text = _lobby.Name;
            lobbyDetailsTexts[1].text = (_lobby.MaxPlayers - _lobby.AvailableSlots).ToString() + "/" + _lobby.MaxPlayers.ToString();
            newLobbyInfo.GetComponentInChildren<Button>().onClick.AddListener(()=>JoinLobby(_lobby.Id)); // We will call join lobby 
        }
    }

    private async void JoinLobby(string _lobbyId)
    {
        try
        {
            JoinLobbyByIdOptions options = new JoinLobbyByIdOptions
            {
                Player = GetPlayer()
            };
            currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(_lobbyId, options);
            EnterRoom();
            Debug.Log("Players in room : " + currentLobby.Players.Count);
        }catch(LobbyServiceException e)
        {
            Debug.Log(e.ErrorCode);
            
        }
    }


    private async void JoinLobbyWithCode()
    {
        string _lobbyCode = roomCodeIF.text;
        try
        {
            JoinLobbyByCodeOptions options = new JoinLobbyByCodeOptions
            {
                Player = GetPlayer()
            };
            currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(_lobbyCode, options);
            EnterRoom();
            Debug.Log("Players in room : " + currentLobby.Players.Count);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }




    private float heartbeattimer = 15f;
    private async void HandleLobbyHeartbeat()
    {
        if (currentLobby != null && IsHost())
        {
            heartbeattimer -= Time.deltaTime;
            if (heartbeattimer <= 0)
            {
                heartbeattimer = 15f;
                await LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
            }
        }
    }

    private bool IsHost()
    {
        if(currentLobby!=null && currentLobby.HostId == playerId)
        {
            return true;
        }
        return false;
    }


    private Player GetPlayer()
    {
        string playerName = PlayerPrefs.GetString("Name");
        if (playerName == null || playerName == "")
            playerName = playerId;
        Player player = new Player
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                {"PlayerName",new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member,playerName) }
            }
        };

        return player;
    }
    

    private async void LeaveRoom()
    {
        try
        {
            await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, playerId);
            currentLobby = null;
            ExitRoom();
        }catch(LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

   private async void KickPlayer(string _playerId)
    {
        try
        {
            await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, _playerId);
        }catch(LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    private void ExitRoom()
    {
        mainMenuPanel.SetActive(true);
        createRoomPanel.SetActive(false);
        joinRoomPanel.SetActive(false);
        roomPanel.SetActive(false);
    }


    private async void StartGame()
    {
        if (currentLobby != null && IsHost())
        {
            try
            {
                UpdateLobbyOptions updateoptions = new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                         {"IsGameStarted", new DataObject(DataObject.VisibilityOptions.Member,"true") }
                    }
                };

                currentLobby = await LobbyService.Instance.UpdateLobbyAsync(currentLobby.Id, updateoptions);

                EnterGame();

            }
            catch(LobbyServiceException e)
            {
                Debug.Log(e);
            }
            
        }
        
    }



    private bool IsGameStarted()
    {
        if (currentLobby != null)
        {
            if (currentLobby.Data["IsGameStarted"].Value == "true")
            {
                return true;
            }
        }
        return false;
    }


   private void EnterGame()
    {
        gamePanel.SetActive(true);
        // Or load another scene
    }






  


}
