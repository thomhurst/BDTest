// @ts-check
/** @type {import('prism-react-renderer').PrismTheme} */
const bdtestPrismTheme = {
  plain: {
    color: '#dce6ec',
    backgroundColor: '#101a22',
  },
  styles: [
    {types: ['comment', 'prolog', 'doctype', 'cdata'], style: {color: '#7d8f9b', fontStyle: 'italic'}},
    {types: ['keyword', 'builtin', 'constant', 'boolean'], style: {color: '#7cc4ee'}},
    {types: ['function', 'class-name', 'attr-name'], style: {color: '#8fe0b0'}},
    {types: ['string', 'char', 'attr-value', 'number'], style: {color: '#f0c97a'}},
    {types: ['tag', 'selector', 'variable'], style: {color: '#c8d6df'}},
    {types: ['inserted'], style: {color: '#8fe0b0'}},
    {types: ['deleted'], style: {color: '#ff9a90'}},
    {types: ['changed'], style: {color: '#f0c97a'}},
  ],
};

/**
 * Docusaurus renders a "Direct link to ..." anchor inside every heading. Converting the
 * built HTML back to Markdown would carry those into each emitted .md file and into
 * llms-full.txt, so drop them before the conversion runs.
 *
 * @returns {(tree: import('hast').Root) => void}
 */
function rehypeStripHeadingAnchors() {
  const isHashLink = (/** @type {any} */ node) =>
    node.type === 'element' &&
    node.tagName === 'a' &&
    []
      .concat(node.properties?.className ?? [])
      .includes('hash-link');

  return (tree) => {
    const walk = (/** @type {any} */ node) => {
      if (!Array.isArray(node.children)) {
        return;
      }
      node.children = node.children.filter((child) => !isHashLink(child));
      node.children.forEach(walk);
    };
    walk(tree);
  };
}

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'BDTest',
  tagline: 'Behaviour-driven tests and reports in plain C#',
  favicon: 'img/favicon.svg',

  url: 'https://thomhurst.github.io',
  baseUrl: '/BDTest/',

  organizationName: 'thomhurst',
  projectName: 'BDTest',
  trailingSlash: false,

  headTags: [
    {
      tagName: 'script',
      attributes: {},
      innerHTML: `window.tlumaConfig = {
  source: 'thomhurst/BDTest',
  theme: 'auto',
  brandColor: 'slate',
  button: 'bottom-right',
  welcomePulse: true,
  edgePadding: '1rem',
  autoOpen: false,
  desktopFullscreenByDefault: false,
};`,
    },
  ],

  scripts: [
    {
      src: 'https://tluma.ai/widget.js',
      async: true,
    },
  ],

  onBrokenLinks: 'throw',

  plugins: [
    [
      '@signalwire/docusaurus-plugin-llms-txt',
      /** @type {import('@signalwire/docusaurus-plugin-llms-txt').PluginOptions} */
      ({
        siteTitle: 'BDTest',
        siteDescription:
          'BDTest is a code-only behaviour-driven testing and reporting framework for .NET. Write Given/When/Then scenarios in C# with NUnit, xUnit, MSTest or TUnit, and publish readable reports.',
        depth: 2,
        enableDescriptions: true,
        content: {
          enableMarkdownFiles: true,
          enableLlmsFullTxt: true,
          // Relative links omit the /BDTest/ baseUrl, which 404s once llms.txt is fetched away from the site.
          relativePaths: false,
          includeDocs: true,
          includeBlog: false,
          includePages: false,
          beforeDefaultRehypePlugins: [rehypeStripHeadingAnchors],
        },
      }),
    ],
  ],

  markdown: {
    hooks: {
      onBrokenMarkdownLinks: 'throw',
    },
  },

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  presets: [
    [
      'classic',
      /** @type {import('@docusaurus/preset-classic').Options} */
      ({
        docs: {
          sidebarPath: './sidebars.js',
          editUrl: 'https://github.com/thomhurst/BDTest/tree/main/docs/',
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      }),
    ],
  ],

  themeConfig:
    /** @type {import('@docusaurus/preset-classic').ThemeConfig} */
    ({
      image: 'img/logo.png',
      colorMode: {
        respectPrefersColorScheme: true,
      },
      navbar: {
        title: 'BDTest',
        logo: {
          alt: 'BDTest Logo',
          src: 'img/logo.svg',
          srcDark: 'img/logo-dark.svg',
        },
        items: [
          {
            type: 'docSidebar',
            sidebarId: 'docsSidebar',
            position: 'left',
            label: 'Documentation',
          },
          {
            to: '/docs/report-server/overview',
            label: 'Report Server',
            position: 'left',
          },
          {
            href: 'https://www.nuget.org/packages/BDTest',
            label: 'NuGet',
            position: 'right',
          },
          {
            href: 'https://github.com/thomhurst/BDTest',
            label: 'GitHub',
            position: 'right',
          },
          {
            href: 'https://github.com/sponsors/thomhurst',
            label: '❤️ Sponsor',
            position: 'right',
          },
        ],
      },
      footer: {
        style: 'dark',
        links: [
          {
            title: 'Docs',
            items: [
              {label: 'Getting Started', to: '/docs/getting-started/installation'},
              {label: 'Writing Tests', to: '/docs/writing-tests/scenarios'},
              {label: 'Test Frameworks', to: '/docs/frameworks/overview'},
              {label: 'Reports', to: '/docs/reports/overview'},
            ],
          },
          {
            title: 'More',
            items: [
              {label: 'GitHub', href: 'https://github.com/thomhurst/BDTest'},
              {label: 'NuGet', href: 'https://www.nuget.org/packages/BDTest'},
              {label: 'Release notes', href: 'https://github.com/thomhurst/BDTest/blob/main/RELEASE_NOTES.md'},
            ],
          },
        ],
        copyright: `Copyright © ${new Date().getFullYear()} Tom Longhurst. Built with Docusaurus.`,
      },
      prism: {
        theme: bdtestPrismTheme,
        darkTheme: bdtestPrismTheme,
        additionalLanguages: ['csharp', 'bash', 'powershell', 'json', 'yaml'],
      },
    }),
};

export default config;
