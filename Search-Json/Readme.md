## .env file structure


### Setup

1. Create Azure OpenAI, Search and Azure AI Services.
2. Install required libraries:

```
pip install -r requirements.txt
```

3. In Azure Search create a data source pointing at your blob container with pdf files
3. Create a .env file in the root folder (where all the json files are). Update with your settings.

```
DATA_SOURCE_NAME=...
INDEX_NAME=...
INDEXER_NAME=...
SKILLSET_NAME=...
OPENAI_API_KEY=...
AI_SERVICE_KEY=...
BLOB_CONNECTION_STRING=...
BLOB_CONTAINER_NAME=...
```

4. Run preFiles.py. It will replace symbolic values from the .env file create updated files in the ./debug directory. Use Azure portal and the json files to create the various articfacts in the following sequence:

- Index
- Skillset
- Indexer



.