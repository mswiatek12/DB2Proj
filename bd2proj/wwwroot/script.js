document.getElementById("options").addEventListener("submit", async function(e) {
    e.preventDefault();
    const option = document.getElementById('option').value;
    
    switch(option) {
        case 'get-all-docs':
            fetch('/api/xmlapi')
    }
})