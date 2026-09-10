import React from 'react'; import { createRoot } from 'react-dom/client'; import './style.css';
function App() { return <main><h1>Dionysus</h1><p>Vite + React frontend готов.</p></main>; }
createRoot(document.getElementById('root')!).render(<React.StrictMode><App /></React.StrictMode>);
