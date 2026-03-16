# Introduction to Agentic AI

Agentic AI refers to AI systems that can autonomously take sequences of actions
to accomplish goals. Unlike single-turn AI assistants, agents can plan, use tools,
and iterate until a task is complete.

## Key characteristics

- **Goal-directed**: the agent works toward an objective, not just a single response
- **Tool use**: the agent can call functions, search files, or query APIs
- **Multi-step reasoning**: complex tasks are broken into sub-steps
- **Self-evaluation**: the agent checks its own output before finishing

## Retrieval-Augmented Generation (RAG)

RAG is a technique where an agent retrieves relevant documents before generating
a response. This grounds the answer in actual data rather than model knowledge alone.

### Agentic RAG

In Agentic RAG the retrieval step is itself agentic:

1. The agent decides *which* queries to run (not just one fixed query)
2. It may search multiple times with different keywords
3. It reads only the most relevant sections
4. It synthesises findings across documents

## Benefits

- Answers are grounded in real documents
- The model can handle documents that exceed its context window
- Multi-hop reasoning across many files becomes possible
