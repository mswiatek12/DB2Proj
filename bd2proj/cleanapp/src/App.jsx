import { useState } from 'react'
import './App.css'

function App() {
    const [showView, setShowView] = useState(false)
    const [showInsert, setShowInsert] = useState(false)
    const [documents, setDocuments] = useState([])
    const [singleDocumentId, setSingleDocumentId] = useState("")
    const [error, setError] = useState(null)
    const [name, setName] = useState("")
    const [xmlContent, setXmlContent] = useState("")
    const [createDate, setCreateDate] = useState("")
    const [deleteId, setDeleteId] = useState("")
    const [searchParams, setSearchParams] = useState({ name: "", node: "", attribute: "", attributeValue: "" })
    const [xpath, setXPath] = useState("");
    const [newValue, setNewValue] = useState("");


    const updateXPathValue = async () => {
        if (!singleDocumentId) {
            setError("Provide document ID");
            return;
        }
        if (!xpath) {
            setError("Provide XPath");
            return;
        }

        try {
            const response = await fetch(`/api/xmlapi/${singleDocumentId}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ XPath: xpath, NewValue: newValue })
            });

            if (!response.ok) {
                const err = await response.json();
                throw new Error(err.Message || "Failed to update document");
            }

            const msg = await response.text();
            setError(null);
            alert(msg); // lub inna informacja o sukcesie
            fetchSingleDocument(); // odśwież dokument po update
        } catch (e) {
            setError(e.message);
        }
    }
    const fetchSingleDocument = async () => {
        try {
            const response = await fetch(`/api/xmlapi/${singleDocumentId}`)
            const json = await response.json()
            setDocuments([JSON.stringify(json, null, 2)])
            setShowView(true)
            setShowInsert(false)
            setError(null)
        } catch {
            setError("Failed to fetch document.")
        }
    }

    const fetchAllDocuments = async () => {
        try {
            const response = await fetch(`/api/xmlapi`)
            const json = await response.json()
            setDocuments([JSON.stringify(json, null, 2)])
            setShowView(true)
            setShowInsert(false)
            setError(null)
        } catch {
            setError("Failed to fetch documents.")
        }
    }

    const insertDocument = async () => {
        try {
            const response = await fetch('/api/xmlapi', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    name,
                    xmlContent,
                    createData: createDate
                })
            })
            const data = await response.json()
            setDocuments([JSON.stringify(data, null, 2)])
            setShowView(true)
            setShowInsert(false)
            setError(null)
        } catch {
            setError("Failed to insert document.")
        }
    }

    const deleteDocument = async () => {
        try {
            const response = await fetch(`/api/xmlapi/${deleteId}`, { method: 'DELETE' })
            if (response.status === 204) {
                setDocuments([`Document with ID ${deleteId} deleted.`])
                setError(null)
            } else {
                setError(`Could not delete document with ID ${deleteId}.`)
            }
        } catch {
            setError("Failed to delete document.")
        }
    }

    const transformDocument = async () => {
        try {
            if (!singleDocumentId || isNaN(Number(singleDocumentId))) {
                setError("Please enter a valid numeric document ID");
                return;
            }

            const response = await fetch(`/api/xmlapi/${singleDocumentId}/transform`);
            if (!response.ok) {
                const errorData = await response.json();
                throw new Error(errorData?.message || "Failed to transform document");
            }

            const html = await response.text();
            setDocuments([html]);
            setShowView(true);
            setShowInsert(false);
        } catch (err) {
            setError(err.message || "Transformation failed.");
        }
    };

    const searchDocuments = async () => {
        const query = new URLSearchParams(searchParams).toString()
        try {
            const response = await fetch(`/api/xmlapi/search?${query}`)
            const json = await response.json()
            setDocuments([JSON.stringify(json, null, 2)])
            setShowView(true)
            setError(null)
        } catch {
            setError("Failed to search documents.")
        }
    }

    return (
        <div className="container">
            <div className="left-side">
                <input type="text" placeholder="Document ID" value={singleDocumentId} onChange={e => setSingleDocumentId(e.target.value)} />
                <button className="button" onClick={fetchSingleDocument}>SEARCH FOR A DOCUMENT</button>
                <button className="button" onClick={fetchAllDocuments}>GET ALL DOCUMENTS</button>
                <button className="button" onClick={() => { setShowInsert(true); setShowView(false); setError(null) }}>INSERT A DOCUMENT</button>
                <input type="text" placeholder="ID to delete" value={deleteId} onChange={e => setDeleteId(e.target.value)} />
                <button className="button" onClick={deleteDocument}>DELETE A DOCUMENT</button>
                <button className="button" onClick={transformDocument}>TRANSFORM DOCUMENT (XSLT)</button>
                <h3>Search XML</h3>
                <input type="text" placeholder="Name" onChange={e => setSearchParams({ ...searchParams, name: e.target.value })} />
                <input type="text" placeholder="Node" onChange={e => setSearchParams({ ...searchParams, node: e.target.value })} />
                <input type="text" placeholder="Attribute" onChange={e => setSearchParams({ ...searchParams, attribute: e.target.value })} />
                <input type="text" placeholder="Attribute Value" onChange={e => setSearchParams({ ...searchParams, attributeValue: e.target.value })} />
                <button className="button" onClick={searchDocuments}>SEARCH</button>
            </div>

            <div className="right-side">
                {showInsert && (
                    <section id="insertDocumentSection">
                        <h2>Insert Document</h2>
                        <input type="text" placeholder="Name" value={name} onChange={e => setName(e.target.value)} />
                        <input type="datetime-local" value={createDate} onChange={e => setCreateDate(e.target.value)} />
                        <textarea rows="10" placeholder="XML Content" value={xmlContent} onChange={e => setXmlContent(e.target.value)} />
                        <button className="button" onClick={insertDocument}>SUBMIT</button>
                    </section>
                )}
                {showView && (
                    <section id="documentsViewSection">
                        <h2>Requested Resources:</h2>
                        <div id="documents">
                            {documents.map((doc, index) =>
                                // Jeśli to HTML (np. po transformacji), wstaw jako HTML
                                doc.trim().startsWith('<') ? (
                                    <div key={index} dangerouslySetInnerHTML={{ __html: doc }} />
                                ) : (
                                    <pre key={index} style={{ whiteSpace: 'pre-wrap' }}>{doc}</pre>
                                )
                            )}
                        </div>
                    </section>
                )}
                {showView && (
                    <section>
                        <h3>Edit XML Element by XPath</h3>
                        <input
                            type="text"
                            placeholder="XPath expression"
                            value={xpath}
                            onChange={e => setXPath(e.target.value)}
                        />
                        <textarea
                            rows={4}
                            placeholder="New value"
                            value={newValue}
                            onChange={e => setNewValue(e.target.value)}
                        />
                        <button className="button" onClick={updateXPathValue}>Update XPath Element</button>
                    </section>
                )}
                {error && <p style={{ color: 'red' }}>{error}</p>}
                
            </div>
        </div>
    )
}

export default App
