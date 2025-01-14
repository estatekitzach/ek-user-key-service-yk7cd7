```
Input: EstateKit - Personal Information API (business logic and data access)
WHY - Vision & Purpose
Purpose & Users
The system will provide an API only. 
In order to ensure that the data encrypted in the primary Estate Kit database can’t be decrypted, we will be:
Ensuring that each user has a unique encryption key. 
House the encryption keys for the users in a separate database, only accessible by a single API. 
The encryption keys should be rotated on a regular basis, with the data that’s encrypted being re-encrypted
What - Core Requirements
The API will have methods to:
Generate a text-based encryption key for a single user id, which is then saved to a data store. 
Encrypt an array of strings passed to it, using the encryption key for the specific user id.
Decrypt an array of strings passed to it, using the encryption key for the specific user id.
Rotate user encryption key. This will be used when keys are being regenerated for each user. 

HOW - Planning & Implementation
Technical Foundation
Required Stack Components
Backend: REST API service for database access.  Use .net Core 9 with C#
ORM: Entity Framework 10
Personal data storage database: existing Postgres RDBMS - estatekit DB

Overall system
PaaS: AWS
Container orchestration: AWS EKS
Authentication: AWS Cognito (OAuth)

System Requirements
Performance: load time under 3 seconds
Security: End-to-end encryption, secure authentication, financial regulatory compliance. 
Reliability: 99.9% uptime, daily data synchronization, automated backups
Business Requirements
All calls to the business logic or data APIs must contain valid security tokens from the OAuth provider. 
There should be no calls allowed that are not using OAuth authentication 
Create Encryption Key Method:
Takes in the User ID as a parameter
This will create an asymmetric key using AWS Key Management Service. 
The public key will be held in the user_key table, along with the user id
The method will return a boolean result
Encryption Method:
Takes in the User ID and an array of strings to encrypt
The method will encrypt each string with the user id’s specific key
Returns an array of encrypted strings
Decryption Method
Takes in the User ID and an array of strings to decrypt
The method will decrypt each string with the user id’s specific key
Returns an array of decrypted strings
Rotate user encryption key 
Takes in the User ID and an array of encrypted strings as a name/value pair.
The method will decrypt the encrypted strings. 
The system will then generate a new encryption key
The method will then re-encrypt the decrypt strings with the new encryption key.
The system will then update the user_key table with the new key.
The system will return the list of encrypted strings as a name/value pair.
Architectural Notes
One database will be used for user encryption/decryption keys
This API will leverage AWS’s Key Management Service to generate asymmetric keys. 


Data Structure

Data tables
User_keys
user_id bigint NOT NULL,
Key character varying(200) COLLATE pg_catalog."default" NOT NULL,
	
	User_key_history
id bigint NOT NULL DEFAULT nextval('"Vault".user_key_history_id_seq'::regclass),
user_id bigint NOT NULL,
date date NOT NULL,
value character varying(200) COLLATE pg_catalog."default",
```