DedicatedClientServer-ServerApp
===============================
This repository contains the Stormancer server app that goes with the DedicatedClientServer sample. The application supports:

- Authentication of a client user
- location & startup of a game session for the user
- Startup, authentication  of a dedicated server by the Stormancer game session
- Waiting that the dedicated server is ready to accept connections.
- Connectivity establishment between the client & the dedicated server.

The Client & server code of the sample hides connectivity establishment, including server port selection & negotiation.

Sample server configuration
===========================

    {
	    "steam": {
		  "apiKey": "0",
		  "appId": 0,
		  "usemockup": true,
		  "vac": false
	    },
	    "index": "dedicated-sample",
	    "gameSession": {
	  	  "usep2p": true
	    },
	    "gameServer": {
	  	  "log": true,
	  	  "executable": "D:\\Intrepid\\20171207\\Sample_05_DedicatedClientServer.exe"
	    }
    }
