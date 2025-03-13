# SmschallengeClient

As this is a challenge for a job application, I didn't want to spend time setting up a CI/CD pipeline, so I just built and ran it locally in Visual Studio.

It should monitor (with rudimentary visuals) the messages sent to the API per second, various information about the accounts and phone numbers associated with those messages, and the successes/failures the final API sent back.

Successes and failures at the moment are only for that endpoint, since it would be different from breaching the maximum limit per second for phone numbers and account numbers (which should be implemented).

The project is divided into two main parts: the client and the server. The client is a console application that sends messages to the server, and the server is an API that receives those messages and sends them to the final API.

There is a small test suite for exceeding limits as well as cleaning up resources when they get too old (1 minute in this case, although all limits and intervals are constants that can be changed)

There is also a filtering system on the front end to filter messages by phone number, account number, and date range.

If there are any questions, please ask. I can give a thorough thought process on why I did things the way I did, and what I would do differently if I had more time or was in a more stable/company environment.
