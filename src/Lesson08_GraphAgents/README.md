# Lesson 08 – Graph RAG Agent

Graph RAG agent backed by a Neo4j knowledge graph with hybrid search and entity exploration.

Port of [02_03_graph_agents](https://github.com/i-am-alice/4th-devs/tree/main/02_03_graph_agents) (i-am-alice/4th-devs).

## Run

```
cd src/Lesson08_GraphAgents
dotnet run
```

## Required setup

1. Copy `App.config.example` to `App.config` and fill in your keys.
2. Set one AI API key: `OPENAI_API_KEY` or `OPENROUTER_API_KEY`.
3. Run **Neo4j 5.11+** (needed for vector index support):

```
docker run -d --name neo4j -p 7474:7474 -p 7687:7687 -e NEO4J_AUTH=neo4j/password neo4j:5
```

4. Set Neo4j credentials in `App.config`:

```xml
<add key="NEO4J_URI"      value="bolt://localhost:7687" />
<add key="NEO4J_USERNAME" value="neo4j" />
<add key="NEO4J_PASSWORD" value="password" />
```

## What it does

1. Indexes `.md`/`.txt` files from `workspace/` — chunks text, extracts entities and relationships via LLM, embeds everything, writes to Neo4j
2. **search** — hybrid full-text + vector retrieval with entity mentions
3. **explore** — traverse an entity's neighborhood in the graph
4. **connect** — find shortest paths between two entities
5. **cypher** — read-only Cypher queries for structural questions
6. **learn / forget** — add or remove documents at runtime
7. **audit / merge_entities** — graph quality maintenance

## REPL commands

| Command | Description |
|---------|-------------|
| `exit` / `quit` | Exit the application |
| `clear` | Clear conversation history and reset stats |
| `reindex` | Re-scan workspace and index any new/changed files |
| `reindex --force` | Wipe the graph and re-index everything from scratch |

## Workspace

Place `.md` or `.txt` files in `workspace/` to make them searchable.
An `example.md` file is included to demonstrate the indexing pipeline.

Documents are indexed automatically on startup. Changed files are re-indexed; deleted files are pruned.
