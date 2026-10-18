// Run with node scripts/Test-HtmlReportControls.cjs. No browser dependencies required.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../BDTest/Helpers/BDTestHtmlHelper.cs'), 'utf8');
const script = source.match(/private const string EmbeddedScript = """\s*<script>([\s\S]*?)<\/script>/)[1];

function verify(grouped) {
  const events = {};
  const controls = {};
  function control() {
    return { hidden: false, textContent: '', value: '', addEventListener(name, handler) { this[name] = handler; } };
  }
  for (const id of ['q', 'tag', 'sort', 'group', 'showing', 'no-results', 'more-label']) controls['#' + id] = control();
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
        'data-tags': JSON.stringify([i % 2 ? 'other' : 'api\tintegration']),
        'data-story': String(this.story), 'data-status': i % 2 ? 'failed' : 'passed',
        'data-i': String(i), 'data-ms': String(i), 'data-sev': i % 2 ? '0' : '1'
      }[name];
    },
    querySelector: () => ({ textContent: 'Scenario ' + i }),
    querySelectorAll: () => []
  }));
  const root = {
    querySelector: selector => controls[selector],
    querySelectorAll: selector => selector === '.story' ? stories : selector === '.scn' ? elements : [],
    setAttribute() {}, addEventListener() {}
  };
  const context = vm.createContext({
    document: { querySelector: () => root },
    window: { addEventListener(name, handler) { events[name] = handler; } }
  });
  // Fail on the former quadratic membership lookup, without timing-sensitive assertions.
  vm.runInContext(`Array.prototype.indexOf = function (value) {
    if (value && value.el) throw new Error('Linear scenario membership lookup');
    for (var i = 0; i < this.length; i++) if (this[i] === value) return i;
    return -1;
  };`, context);
  vm.runInContext(script, context);
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

  controls['#tag'].value = 'api\tintegration';
  controls['#tag'].change();
  assert.equal(shown().length, 200);
  events.beforeprint();
  assert.equal(shown().length, 325, 'Print respects active tag filter');
  assert.ok(shown().every(el => el.getAttribute('data-status') === 'passed'));
  events.afterprint();
  assert.equal(shown().length, 200);

  controls['#q'].value = 'Scenario 648';
  controls['#q'].input();
  assert.equal(shown().length, 1, 'Search works after printing');
  events.beforeprint();
  assert.equal(shown().length, 1, 'Print respects search');
  events.afterprint();
}

verify(true);
verify(false);
console.log('Report control regressions passed (grouped and flat, pagination, search, tags, print).');
