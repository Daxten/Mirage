using UnityEngine;
using UnityEngine.UI;

namespace Mirage.Examples.Basic
{
    public class Player : NetworkBehaviour
    {
        [Header("Player Components")]
        public RectTransform rectTransform;
        public Image image;

        [Header("Child Text Objects")]
        public Text playerNameText;
        public Text playerDataText;

        // These are set in OnStartServer and used in OnStartClient
        [SyncVar]
        int playerNo;
        [SyncVar]
        Color playerColor;

        private static int playerCounter = 1;

        private static int GetNextPlayerId()
        {
            return playerCounter++;
        }

        // This is updated by UpdateData which is called from OnStartServer via InvokeRepeating
        [SyncVar(hook = nameof(OnPlayerDataChanged))]
        public int playerData;

        void Awake()
        {
            Identity.OnStartServer.AddListener(OnStartServer);
            Identity.OnStartClient.AddListener(OnStartClient);
            Identity.OnStartLocalPlayer.AddListener(OnStartLocalPlayer);
        }

        // This is called by the hook of playerData SyncVar above
        void OnPlayerDataChanged(int oldPlayerData, int newPlayerData)
        {
            // Show the data in the UI
            playerDataText.text = string.Format("Data: {0:000}", newPlayerData);
        }

        // This fires on server when this player object is network-ready
        public void OnStartServer()
        {
            // Set SyncVar values
            playerNo = GetNextPlayerId();
            playerColor = Random.ColorHSV(0f, 1f, 0.9f, 0.9f, 1f, 1f);

            // Start generating updates
            InvokeRepeating(nameof(UpdateData), 1, 1);
        }

        // This only runs on the server, called from OnStartServer via InvokeRepeating
        [Server(error = false)]
        void UpdateData()
        {
            playerData = Random.Range(100, 1000);
        }

        // This fires on all clients when this player object is network-ready
        public void OnStartClient()
        {
            // Calculate position in the layout panel
            int x = 100 + ((playerNo % 4) * 150);
            int y = -170 - ((playerNo / 4) * 80);
            rectTransform.anchoredPosition = new Vector2(x, y);

            // Apply SyncVar values
            playerNameText.color = playerColor;
            playerNameText.text = string.Format("Player {0:00}", playerNo);
        }

        // This only fires on the local client when this player object is network-ready
        public void OnStartLocalPlayer()
        {
            // apply a shaded background to our player
            image.color = new Color(1f, 1f, 1f, 0.1f);
        }
    }
}
