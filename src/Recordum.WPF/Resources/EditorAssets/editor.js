(function () {
  const Editor = toastui.Editor;
  const editor = new Editor({
    el: document.querySelector('#editor'),
    height: '100%',
    initialEditType: 'wysiwyg',
    previewStyle: 'tab',
    usageStatistics: false,
    toolbarItems: [
      ['heading', 'bold', 'italic', 'strike'],
      ['ul', 'ol', 'task'],
      ['hr', 'quote'],
      ['table', 'code', 'codeblock'],
      ['link']
    ]
  });

  // Suppress ALL change events emitted while we are applying remote content.
  // Toast UI's setMarkdown() fires multiple change events synchronously (one with
  // the old doc, one with the new); a one-shot flag leaked the trailing event back
  // through the two-way binding, and because getMarkdown() normalises markdown
  // (its output never byte-equals the input) the idempotency guards never converged
  // -> infinite feedback loop. Scoping the flag to the setMarkdown() call swallows
  // every event it emits, regardless of count, while leaving genuine edits to flow.
  let applyingRemote = false;

  editor.on('change', () => {
    if (applyingRemote) return;
    window.chrome.webview.postMessage({ type: 'change', md: editor.getMarkdown() });
  });

  window.chrome.webview.addEventListener('message', (event) => {
    const data = event.data;
    if (!data || typeof data !== 'object') return;
    switch (data.type) {
      case 'setContent':
        if (editor.getMarkdown() !== (data.md || '')) {
          applyingRemote = true;
          try { editor.setMarkdown(data.md || ''); }
          finally { applyingRemote = false; }
        }
        break;
      case 'setReadOnly': {
        // Toast UI doesn't have a true read-only mode; emulate by disabling input.
        const ui = document.querySelector('.toastui-editor-defaultUI');
        if (ui) ui.style.pointerEvents = data.value ? 'none' : '';
        break;
      }
    }
  });

  // Signal ready so the .NET side can flush its initial setContent.
  window.chrome.webview.postMessage({ type: 'ready' });
})();
