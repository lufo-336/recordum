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
      ['ul', 'ol'],
      ['hr', 'quote', 'code'],
      ['link']
    ]
  });

  let suppressNext = false;

  editor.on('change', () => {
    if (suppressNext) { suppressNext = false; return; }
    const md = editor.getMarkdown();
    window.chrome.webview.postMessage({ type: 'change', md: md });
  });

  window.chrome.webview.addEventListener('message', (event) => {
    const data = event.data;
    if (!data || typeof data !== 'object') return;
    switch (data.type) {
      case 'setContent':
        if (editor.getMarkdown() !== data.md) {
          suppressNext = true;
          editor.setMarkdown(data.md || '');
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
