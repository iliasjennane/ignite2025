window.speech = {
  start: (dotnetObjRef) => {
    const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SpeechRecognition) { dotnetObjRef.invokeMethodAsync('OnSpeechError', 'Not supported'); return; }
    const rec = new SpeechRecognition();
    rec.lang = 'en-US';
    rec.interimResults = true;
    rec.onresult = (e) => {
      let transcript = '';
      for (let i = e.resultIndex; i < e.results.length; i++) transcript += e.results[i][0].transcript;
      dotnetObjRef.invokeMethodAsync('OnSpeechInterim', transcript);
    };
    rec.onend = () => dotnetObjRef.invokeMethodAsync('OnSpeechEnd');
    rec.onerror = (e) => dotnetObjRef.invokeMethodAsync('OnSpeechError', e.error);
    rec.start();
    window._rec = rec;
  },
  stop: () => { if (window._rec) window._rec.stop(); }
};