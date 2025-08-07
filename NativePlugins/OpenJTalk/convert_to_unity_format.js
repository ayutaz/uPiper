// Convert ES6 module to Unity WebGL compatible format
const fs = require('fs');
const path = require('path');

const inputFile = path.join(__dirname, '../../Assets/uPiper/Plugins/WebGL/openjtalk.js');
const outputFile = inputFile;

// Read the ES6 module
let content = fs.readFileSync(inputFile, 'utf8');

// Replace ES6 export with a global function
content = content.replace(
    /export\s+default\s+async\s+function\s+OpenJTalkModule/,
    'var OpenJTalkModule = (async function OpenJTalkModuleFactory'
);

// Add closing parentheses and make it self-executing
content = content.replace(
    /OpenJTalkModule\..+$/m,
    '$&);'
);

// Wrap in IIFE and expose as global
const wrappedContent = `(function() {
    ${content}
    
    // Expose as global for Unity
    if (typeof window !== 'undefined') {
        window.OpenJTalkModule = OpenJTalkModule;
    }
    if (typeof self !== 'undefined') {
        self.OpenJTalkModule = OpenJTalkModule;
    }
})();
`;

// Write the converted file
fs.writeFileSync(outputFile, wrappedContent);
console.log('Converted OpenJTalk module to Unity WebGL format');