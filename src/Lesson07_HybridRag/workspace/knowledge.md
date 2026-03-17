# Retrieval-Augmented Generation (RAG)

## What is RAG?

Retrieval-Augmented Generation (RAG) is an AI technique that combines a language model
with an external knowledge base. Instead of relying solely on the model's parametric
memory (what it learned during training), RAG retrieves relevant documents at query
time and feeds them into the model's context.

This approach addresses two key limitations of standalone LLMs:
- **Knowledge cutoff**: Training data has a fixed date; new facts are unavailable.
- **Hallucination**: Models sometimes invent facts not present in their training data.

## Hybrid Search

Hybrid search combines two complementary retrieval strategies:

### Full-Text Search (FTS5 / BM25)

BM25 (Best Match 25) is a ranking function used by search engines. It scores
documents based on keyword frequency (TF) and inverse document frequency (IDF).
SQLite's FTS5 extension implements BM25 natively and is very fast for keyword lookups.

Strengths of FTS5/BM25:
- Exact keyword matching — great for proper nouns, technical terms, IDs
- No API calls required — purely local computation
- Handles uncommon or low-frequency terms well

### Vector Similarity Search

Vector search converts text into high-dimensional embedding vectors using a neural
network (e.g., OpenAI's `text-embedding-3-small` with 1536 dimensions). Documents
and queries are embedded, and the most semantically similar documents are retrieved
via cosine similarity.

Strengths of vector search:
- Understands semantic meaning — "car" matches "automobile"
- Handles paraphrasing and synonym variations
- Good for conceptual questions, not just exact terms

## Reciprocal Rank Fusion (RRF)

RRF is a simple, effective algorithm for combining ranked result lists from multiple
retrieval methods. For each document, its score is:

```
RRF(d) = sum over lists of 1 / (k + rank(d))
```

where `k` is a constant (typically 60) and `rank(d)` is the document's position
in that list (1-indexed).

RRF is robust, parameter-free, and consistently outperforms score normalisation
methods in information retrieval benchmarks.

## Chunking Strategies

Before indexing, documents must be split into manageable pieces (chunks).
The choice of strategy affects retrieval quality:

### Character-based chunking
Splits text into fixed-size windows (e.g., 1000 characters) with overlap (e.g., 200).
Simple and fast, but may split sentences mid-way.

### Separator-based chunking
Recursively splits on a hierarchy of separators: headings → paragraphs → sentences → words.
Produces more semantically coherent chunks. Supports overlap to preserve cross-boundary context.

### Context-enriched chunking (Anthropic-style)
Runs separator chunking first, then asks an LLM to write a 1-2 sentence context prefix
for each chunk. The prefix situates the chunk within the broader document, improving
retrieval recall.

### Topic-based chunking
Asks an LLM to identify logical topic boundaries in the document and return one chunk
per topic. Produces the most semantically pure chunks but is the most expensive.

## Embeddings

An embedding is a dense vector representation of text that captures its meaning.
Similar texts produce vectors that are geometrically close in the embedding space.

OpenAI's `text-embedding-3-small` produces 1536-dimensional vectors.
Cosine similarity between two vectors ranges from -1 (opposite) to 1 (identical).
In practice, semantically similar text typically scores 0.6 – 0.9.
