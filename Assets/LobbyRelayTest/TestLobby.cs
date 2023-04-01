using System.Collections;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class TestLobby : MonoBehaviour
{
    private Lobby hostLobby;

    // Start is called before the first frame update
    async void Start()
    {
        await UnityServices.InitializeAsync();
        AuthenticationService.Instance.SignedIn += ()=>{
            Debug.Log("Signed In" + AuthenticationService.Instance.PlayerId);
        };
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

       // StartCoroutine(SendHeartBeatAsync());
    }

    public async void CreateLobby()
    {
        try
        {
            string lobbyName = "Lobby Name : My Lobby";
            int maxPlayers = 4;
            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers);
            hostLobby = lobby;
            Debug.Log("Lobby Crearted " + lobby.Name);
        }
        catch(LobbyServiceException e)
        {
            Debug.Log(e);
        }
        
    }

    private float heartBeatTimer = 15f;

    private async void Update()
    {
        if (hostLobby != null)
        {
            heartBeatTimer -= Time.deltaTime;
            if (heartBeatTimer <= 0)
            {
                heartBeatTimer = 15;
                await LobbyService.Instance.SendHeartbeatPingAsync(hostLobby.Id);
            }
        }
    }

    public async void ListLobbies()
    {
        QueryResponse queryResponse =  await Lobbies.Instance.QueryLobbiesAsync();

        foreach(Lobby lobby in queryResponse.Results)
        {
            Debug.Log("Lobby Name : " + lobby.Name + " Max Players : " + lobby.MaxPlayers);
        }
    }



}
