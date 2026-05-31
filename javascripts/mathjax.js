window.MathJax = {
  tex: {
    inlineMath: [["\\(", "\\)"], ["$", "$"]],
    displayMath: [["\\[", "\\]"], ["$$", "$$"]],
    processEscapes: true,
    processEnvironments: true
  }
};

// admonition / callout が展開された後も MathJax を再実行する
document$.subscribe(() => {
  MathJax.typesetPromise();
});
