/** @type {import('@docusaurus/plugin-content-docs').SidebarsConfig} */
const sidebars = {
  docsSidebar: [
    {
      type: 'category',
      label: 'Start Here',
      collapsed: false,
      items: [
        'intro',
        'getting-started/installation',
        'getting-started/first-test',
        'getting-started/example-project',
      ],
    },
    {
      type: 'category',
      label: 'Writing Tests',
      items: [
        'writing-tests/scenarios',
        'writing-tests/test-context',
        'writing-tests/step-classes',
        'writing-tests/step-text',
        'writing-tests/stories-and-scenarios',
        'attributes/story-text',
        'writing-tests/tags-and-information',
        'writing-tests/async-steps',
        'writing-tests/scenario-outlines',
        'writing-tests/timeouts',
        'writing-tests/retries',
        'configuration/retry-hook-errors',
        'writing-tests/skipping',
        'writing-tests/step-hooks',
        'writing-tests/output',
      ],
    },
    {
      type: 'category',
      label: 'Test Frameworks',
      items: [
        'frameworks/overview',
        'frameworks/tunit',
        'frameworks/nunit',
        'frameworks/xunit',
        'frameworks/mstest',
        'frameworks/standalone',
        'frameworks/composition',
        'frameworks/custom-adapters',
      ],
    },
    {
      type: 'category',
      label: 'Configuration',
      items: [
        'configuration/settings',
        'configuration/exceptions',
        'configuration/statuses',
      ],
    },
    {
      type: 'category',
      label: 'Reports',
      items: [
        'reports/overview',
        'reports/html-report',
        'reports/html-report-security',
        'reports/json-data',
        'reports/ci-integration',
      ],
    },
    {
      type: 'category',
      label: 'Report Server',
      items: [
        'report-server/overview',
        'report-server/setup',
        'report-server/sending-results',
        'report-server/persistence',
        'report-server/security',
        'report-server/customization',
        'report-server/hosting',
        'report-server/caching-and-limits',
        'report-server/sample-server',
      ],
    },
    {
      type: 'category',
      label: 'Advanced',
      items: [
        'advanced/explicit-runs',
        'advanced/utilities',
        'advanced/source-generator',
      ],
    },
    {
      type: 'category',
      label: 'Migration',
      items: [
        'migration/delegate-steps',
        'migration/async-only',
        'migration/statuses',
        'migration/json-serialization',
        'migration/report-storage',
        'migration/from-specflow',
      ],
    },
    'contributing',
  ],
};

export default sidebars;
