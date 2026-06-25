const assert = require('node:assert/strict');
const test = require('node:test');

global.window = global;
global.navigator = {};
global.document = {};

require('./markdown.js');

test('csharp code fences render with styled C# headers and copy controls', () => {
    const { html } = window.YokiMarkdown.renderWithHeadings([
        '```csharp',
        'public class Demo',
        '{',
        '    public void Play() => mBlack.PlayAnimation("idle");',
        '}',
        '```',
    ].join('\n'));

    assert.match(html, /class="md-code-header"/);
    assert.match(html, /<span class="md-code-lang">C#<\/span>/);
    assert.match(html, /class="md-copy-btn"/);
    assert.doesNotMatch(html, /md-codeblock__bar/);
    assert.doesNotMatch(html, /<span class="md-code-lang">csharp<\/span>/);
});

test('syntax highlighter keeps protected strings intact while highlighting numeric literals', () => {
    const html = window.YokiMarkdown.highlight([
        'public class Demo',
        '{',
        '    private const string Label = "State 0";',
        '    private PlayerState State = PlayerState.Idle;',
        '    private int Count = 42;',
        '    protected override void OnEnter() => PlayAnimation("idle");',
        '}',
    ].join('\n'), 'csharp');

    assert.match(html, /tok-keyword/);
    assert.match(html, /tok-type/);
    assert.match(html, /tok-method/);
    assert.match(html, /<span class="tok-string">&quot;State 0&quot;<\/span>/);
    assert.match(html, /<span class="tok-number">42<\/span>/);
    assert.doesNotMatch(html, /class="tok-number">0/);
    assert.doesNotMatch(html, /\x00/);
});

test('inline code renders generic brackets without double escaping', () => {
    const { html } = window.YokiMarkdown.renderWithHeadings(
        '状态基类使用 `AbstractState<TEnum, TBlack>` 提供生命周期方法。'
    );

    assert.match(html, /<code class="md-code-inline">AbstractState&lt;TEnum, TBlack&gt;<\/code>/);
    assert.doesNotMatch(html, /&amp;lt;/);
    assert.doesNotMatch(html, /&amp;gt;/);
});
