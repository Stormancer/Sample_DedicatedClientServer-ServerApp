DedicatedClientServer-ServerApp
===============================
This repository contains the Stormancer server app that goes with the DedicatedClientServer sample. The application supports:

- Authentication of a client user
- location & startup of a game session for the user
- Startup, authentication  of a dedicated server by the Stormancer game session
- Waiting that the dedicated server is ready to accept connections.
- Connectivity establishment between the client & the dedicated server.

The Client & server code of the sample hides connectivity establishment, including server port selection & negotiation.

Usage
=====
Deploy this repository to a Stormancer server application and add the following json to the server config.

Replace the `gameServer.executable` config value with a path to the dedicated server to start.

Sample server configuration
===========================

    {
	"auth": {
		"test": {
			"enabled": true, //Use test login provider
			"blackList": [
				"badLogin"
			]
		}
	},
	"index": "dedicated-sample", //Index used to store use accounts.
	"gameSession": {
		"usep2p": true
	},
	"gameServer": {
		"log": true,
		"executable": "c:\\<path to dedicated server exe>"
	  }
    }
