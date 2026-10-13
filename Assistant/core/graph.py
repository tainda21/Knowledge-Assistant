from langgraph.graph import END, START, StateGraph

from core.nodes.generate_node import generate_response
from core.nodes.grade_documents_node import grade_documents
from core.nodes.retrieve_node import retrieve_documents
from core.prompts import SYSTEM_INSTRUCTION
from core.state import AgentState

# Compatibility alias for callers that previously imported the preparation node from this module.
prepare_generation = generate_response


def create_chat_graph():
    workflow = StateGraph(AgentState)
    workflow.add_node("retrieve", retrieve_documents)
    workflow.add_node("grade_documents", grade_documents)
    workflow.add_node("generate", generate_response)
    workflow.add_edge(START, "retrieve")
    workflow.add_edge("retrieve", "grade_documents")
    workflow.add_edge("grade_documents", "generate")
    workflow.add_edge("generate", END)
    return workflow.compile()


chat_graph = create_chat_graph()
