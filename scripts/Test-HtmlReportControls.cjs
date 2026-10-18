// Run with node scripts/Test-HtmlReportControls.cjs. No browser dependencies required.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../BDTest/Helpers/BDTestHtmlHelper.cs'), 'utf8');
const script = source.match(/private const string EmbeddedScript = """\s*<script>([\s\S]*?)<\/script>/)[1];

function verify(grouped, protocol) {
  const events = {};
  const controls = {};
  let reloads = 0;
  const frame = {
    sandbox: 'allow-popups',
    get srcdoc() { return '<p>Evidence</p>'; },
    set srcdoc(value) { assert.equal(value, '<p>Evidence</p>'); reloads++; },
    setAttribute(name, value) { assert.equal(name, 'sandbox'); this.sandbox = value; }
  };
  function control() {
    return { hidden: false, textContent: '', value: '', addEventListener(name, handler) { this[name] = handler; } };
  }
  for (const id of ['q', 'tag', 'sort', 'group', 'showing', 'no-results', 'more-label']) controls['#' + id] = control();
  const unusualTag = 'API\tintegration\nline\rreturn\0null\bback\fform\\slash"quote<>&\'';
  controls['#tag'].options = [{ value: '' }, { value: JSON.stringify(unusualTag) }];
  const moreButton = control();
  controls['#more'] = { hidden: false, querySelector: () => moreButton };
  const flat = { hidden: true, appendChild(el) { el.parentNode = this; } };
  controls['#flat-list'] = flat;
  const stories = Array.from({ length: 3 }, (_, i) => ({
    hidden: false,
    getAttribute: () => String(i),
    querySelector(selector) {
      if (selector === '.story-text') return { textContent: 'Story ' + i };
      return elements.find(el => el.story === i && el.parentNode === homes[i] && !el.hidden) || null;
    }
  }));
  const homes = stories.map(() => ({ appendChild(el) { el.parentNode = this; } }));
  const elements = Array.from({ length: 650 }, (_, i) => ({
    story: Math.floor(i / 250), parentNode: homes[Math.floor(i / 250)], hidden: false,
    getAttribute(name) {
      return {
        'data-tags': JSON.stringify([i % 2 ? 'other' : unusualTag]),
        'data-story': String(this.story), 'data-status': i % 2 ? 'failed' : 'passed',
        'data-i': String(i), 'data-ms': String(i), 'data-sev': i % 2 ? '0' : '1'
      }[name];
    },
    querySelector: () => ({ textContent: 'Scenario ' + i }),
    querySelectorAll: () => []
  }));
  const root = {
    querySelector: selector => controls[selector],
    querySelectorAll: selector => selector === '.story' ? stories : selector === '.scn' ? elements : selector === 'iframe.custom-body' ? [frame] : [],
    setAttribute() {}, addEventListener(name, handler) { events['root-' + name] = handler; }, contains: () => true
  };
  const context = vm.createContext({
    document: { querySelector: () => root },
    window: { location: { protocol }, addEventListener(name, handler) { events[name] = handler; } }
  });
  // Fail on the former quadratic membership lookup, without timing-sensitive assertions.
  vm.runInContext(`Array.prototype.indexOf = function (value) {
    if (value && value.el) throw new Error('Linear scenario membership lookup');
    for (var i = 0; i < this.length; i++) if (this[i] === value) return i;
    return -1;
  };`, context);
  vm.runInContext(script, context);
  assert.equal(frame.sandbox, protocol === 'file:' ? 'allow-popups allow-same-origin' : 'allow-popups');
  assert.equal(reloads, protocol === 'file:' ? 1 : 0, 'Only file reports reload frames with local-image access');
  const shown = () => elements.filter(el => !el.hidden);
  const visibleStories = () => stories.filter(story => !story.hidden).length;
  assert.equal(shown().length, 200);
  assert.equal(visibleStories(), 1);
  if (!grouped) {
    controls['#group'].checked = false;
    controls['#group'].change();
  }
  events.beforeprint();
  assert.equal(shown().length, 650, 'Print includes scenarios beyond the page limit');
  assert.equal(visibleStories(), grouped ? 3 : 0);
  assert.equal(flat.hidden, grouped);
  events.afterprint();
  assert.equal(shown().length, 200, 'Print restores pagination');

  assert.equal(controls['#tag'].options[1].value, unusualTag.toLowerCase(), 'Tag options preserve control characters after JSON decoding');
  controls['#tag'].value = controls['#tag'].options[1].value;
  controls['#tag'].change();
  assert.equal(shown().length, 200);
  events.beforeprint();
  assert.equal(shown().length, 325, 'Print respects active tag filter');
  assert.ok(shown().every(el => el.getAttribute('data-status') === 'passed'));
  events.afterprint();
  assert.equal(shown().length, 200);

  const tagButton = {
    classList: { contains: () => false },
    hasAttribute: name => name === 'data-tag',
    getAttribute: () => JSON.stringify('other')
  };
  events['root-click']({ target: { closest: () => tagButton } });
  events.beforeprint();
  assert.equal(shown().length, 325, 'Tag buttons match their JSON-decoded values');
  assert.ok(shown().every(el => el.getAttribute('data-status') === 'failed'));
  events.afterprint();
  tagButton.getAttribute = () => JSON.stringify(unusualTag);
  events['root-click']({ target: { closest: () => tagButton } });
  assert.equal(controls['#tag'].value, unusualTag.toLowerCase(), 'Tag button selects matching dropdown option');

  controls['#q'].value = 'Scenario 648';
  controls['#q'].input();
  assert.equal(shown().length, 1, 'Search works after printing');
  events.beforeprint();
  assert.equal(shown().length, 1, 'Print respects search');
  events.afterprint();
}

for (const protocol of ['file:', 'https:']) {
  verify(true, protocol);
  verify(false, protocol);
}
console.log('Report control regressions passed (grouped and flat, pagination, search, tags, print, local/hosted sandbox).');
