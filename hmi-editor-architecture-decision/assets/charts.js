(function() {
  var style = getComputedStyle(document.documentElement);
  var accent = style.getPropertyValue('--accent').trim();
  var accent2 = style.getPropertyValue('--accent2').trim();
  var ink = style.getPropertyValue('--ink').trim();
  var muted = style.getPropertyValue('--muted').trim();
  var rule = style.getPropertyValue('--rule').trim();
  var bg = style.getPropertyValue('--bg').trim();
  var bg2 = style.getPropertyValue('--bg2').trim();

  // --- Chart: Chip Radar Comparison ---
  var radarDom = document.getElementById('chart-chip-radar');
  if (radarDom) {
    var radarChart = echarts.init(radarDom, null, { renderer: 'svg' });
    radarChart.setOption({
      animation: false,
      color: [accent, accent2],
      tooltip: { appendToBody: true },
      legend: {
        data: ['T113-S3', 'RK3506'],
        bottom: 0,
        textStyle: { color: muted }
      },
      radar: {
        center: ['50%', '52%'],
        radius: '65%',
        indicator: [
          { name: 'CPU\u6027\u80fd', max: 10 },
          { name: '2D\u52a0\u901f', max: 10 },
          { name: '\u663e\u793a\u5206\u8fa8\u7387', max: 10 },
          { name: '\u89c6\u9891\u89e3\u7801', max: 10 },
          { name: '\u5de5\u4e1a\u63a5\u53e3', max: 10 },
          { name: '\u542f\u52a8\u901f\u5ea6', max: 10 },
          { name: '\u529f\u8017\u63a7\u5236', max: 10 },
          { name: '\u751f\u6001\u6210\u719f\u5ea6', max: 10 }
        ],
        axisName: { color: muted },
        splitArea: {
          areaStyle: { color: ['rgba(56,189,248,0.02)', 'rgba(56,189,248,0.04)'] }
        },
        splitLine: { lineStyle: { color: rule } },
        axisLine: { lineStyle: { color: rule } }
      },
      series: [{
        type: 'radar',
        name: 'T113-S3',
        data: [{ value: [6, 3, 9, 9, 6, 5, 6, 9], name: 'T113-S3' }],
        symbol: 'circle',
        symbolSize: 6,
        lineStyle: { width: 2 },
        areaStyle: { opacity: 0.15 }
      }, {
        type: 'radar',
        name: 'RK3506',
        data: [{ value: [8, 8, 6, 2, 8, 9, 9, 6], name: 'RK3506' }],
        symbol: 'circle',
        symbolSize: 6,
        lineStyle: { width: 2 },
        areaStyle: { opacity: 0.15 }
      }]
    });
    window.addEventListener('resize', function() { radarChart.resize(); });
  }

  // --- Chart: Editor Feature Comparison ---
  var editorDom = document.getElementById('chart-editor-compare');
  if (editorDom) {
    var editorChart = echarts.init(editorDom, null, { renderer: 'svg' });
    editorChart.setOption({
      animation: false,
      color: [accent, accent2, muted, accent + '99', accent2 + '99', accent + '66', accent2 + '66'],
      tooltip: { appendToBody: true, trigger: 'axis' },
      legend: {
        data: ['SquareLine', 'GUI Guider', 'EEZ Studio', 'TouchGFX', 'Embedded Wizard', 'Qt DS', 'emWin AW'],
        bottom: 0,
        textStyle: { color: muted, fontSize: 10 },
        type: 'scroll'
      },
      grid: { left: '3%', right: '4%', bottom: '15%', top: '10%', containLabel: true },
      xAxis: {
        type: 'category',
        data: ['\u5f00\u6e90\u7a0b\u5ea6', 'LVGL\u517c\u5bb9', '\u8de8\u5e73\u53f0', '\u52a8\u753b\u80fd\u529b', '\u793e\u533a\u89c4\u6a21', '\u4e2d\u6587\u8d44\u6599', '\u6027\u4ef7\u6bd4', '\u53ef\u5b9a\u5236\u6027'],
        axisLabel: { color: muted }
      },
      yAxis: {
        type: 'value',
        max: 10,
        axisLabel: { color: muted },
        splitLine: { lineStyle: { color: rule } }
      },
      series: [
        { name: 'SquareLine', type: 'bar', data: [2, 9, 6, 8, 5, 6, 5, 4] },
        { name: 'GUI Guider', type: 'bar', data: [4, 9, 7, 6, 7, 9, 9, 5] },
        { name: 'EEZ Studio', type: 'bar', data: [9, 9, 7, 4, 4, 2, 9, 9] },
        { name: 'TouchGFX', type: 'bar', data: [3, 1, 4, 8, 8, 9, 7, 3] },
        { name: 'Embedded Wizard', type: 'bar', data: [1, 1, 8, 9, 2, 1, 3, 5] },
        { name: 'Qt DS', type: 'bar', data: [2, 1, 9, 10, 10, 7, 2, 7] },
        { name: 'emWin AW', type: 'bar', data: [3, 1, 7, 4, 5, 5, 5, 3] }
      ]
    });
    window.addEventListener('resize', function() { editorChart.resize(); });
  }
})();
