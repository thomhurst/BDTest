import {useEffect, useState} from 'react';
import Link from '@docusaurus/Link';
import useBaseUrl from '@docusaurus/useBaseUrl';
import Layout from '@theme/Layout';
import Heading from '@theme/Heading';
import CodeBlock from '@theme/CodeBlock';

import styles from './index.module.css';

const testCode = `[Story(AsA = "account holder",
    IWant = "to withdraw money",
    SoThat = "I can use my savings")]
public class WithdrawalTests : TUnitBDTestBase<AccountContext>
{
    private AccountSteps Account => new(Context);

    [Test]
    [ScenarioText("Withdrawing money reduces the balance")]
    public async Task WithdrawMoney()
    {
        await Given(() => Account.CreateAnAccount(100m))
            .When(() => Account.Withdraw(25m))
            .Then(() => Account.TheBalanceIs(75m))
            .BDTestAsync();
    }
}`;

const stepsCode = `public class AccountSteps(AccountContext context)
{
    [StepText("I have an account with a balance of {0}")]
    public void CreateAnAccount(decimal balance) =>
        context.Balance = balance;

    [StepText("I withdraw {0}")]
    public void Withdraw(decimal amount) =>
        context.Balance -= amount;

    [StepText("the account balance is {0}")]
    public async Task TheBalanceIs(decimal expected) =>
        await Assert.That(context.Balance).IsEqualTo(expected);
}`;

// BDTest.TUnit depends only on TUnit.Core; TUnit brings the runner and assertions.
const installCommands = `dotnet add package TUnit
dotnet add package BDTest.TUnit
dotnet add package BDTest.SourceGenerator`;

const reportSteps = [
  ['Given', 'I have an account with a balance of 100'],
  ['When', 'I withdraw 25'],
  ['Then', 'the account balance is 75'],
];

/*
 * The hero report "runs" the scenario: each step goes pending, then passes, then the
 * scenario passes and the cycle restarts. Server render shows the finished report so
 * nothing jumps for users with reduced motion or no JavaScript.
 */
function useRunningStep() {
  const [tick, setTick] = useState(reportSteps.length + 2);
  useEffect(() => {
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      return undefined;
    }
    setTick(0);
    const id = window.setInterval(() => setTick(value => (value + 1) % (reportSteps.length + 4)), 850);
    return () => window.clearInterval(id);
  }, []);
  return tick;
}

function ReportCard() {
  const tick = useRunningStep();
  const done = tick > reportSteps.length;
  return (
    <figure className={styles.report} aria-label="Example BDTest report entry">
      <div className={styles.reportHead}>
        <span className={styles.reportLabel}>Story</span>
        <p>As an <b>account holder</b>, I want <b>to withdraw money</b>, so that <b>I can use my savings</b>.</p>
      </div>
      <div className={styles.reportScenario}>
        <span className={`${styles.badge} ${done ? styles.badgePass : styles.badgeRun}`}>
          {done ? 'Passed' : 'Running'}
        </span>
        <span>Withdrawing money reduces the balance</span>
      </div>
      <ol className={styles.reportSteps}>
        {reportSteps.map(([keyword, text], index) => {
          const state = index < tick ? 'pass' : index === tick ? 'run' : 'idle';
          return (
            <li className={styles[`step_${state}`]} key={keyword}>
              <span className={styles.stepDot} aria-hidden="true" />
              <span className={styles.stepKeyword}>{keyword}</span>
              <span>{text}</span>
            </li>
          );
        })}
      </ol>
    </figure>
  );
}

function Hero() {
  return (
    <header className={styles.hero}>
      <div className={`${styles.container} ${styles.heroGrid}`}>
        <div className={styles.heroCopy}>
          <Heading as="h1" className={styles.headline}>
            Write the test.
            <br />
            <span className={styles.headlineAccent}>Read the report.</span>
          </Heading>
          <p className={styles.heroDescription}>
            BDTest is a behaviour-driven testing framework for .NET with no Gherkin files and no
            generated tests. Your Given / When / Then scenarios are ordinary C#, and their results
            become reports your whole team can read.
          </p>
          <div className={styles.heroActions}>
            <Link className={styles.primaryButton} to="/docs/getting-started/installation">Get started</Link>
            <code className={styles.installChip}>{installCommands}</code>
          </div>
        </div>
        <ReportCard />
      </div>
    </header>
  );
}

function Walkthrough() {
  return (
    <section className={`${styles.container} ${styles.walkthrough}`} aria-labelledby="walkthrough-heading">
      <Heading as="h2" id="walkthrough-heading" className={styles.sectionTitle}>Steps are methods. Scenarios are tests.</Heading>
      <p className={styles.sectionLead}>
        Use your test runner, your IDE and your debugger as usual. The optional source generator
        records which method each step calls. BDTest then reads the attributes and current argument
        values just before each step runs, and writes them into the report.
      </p>
      <div className={styles.codeGrid}>
        <CodeBlock language="csharp" title="WithdrawalTests.cs">{testCode}</CodeBlock>
        <CodeBlock language="csharp" title="AccountSteps.cs">{stepsCode}</CodeBlock>
      </div>
    </section>
  );
}

const reasons = [
  ['No Gherkin, no generated tests', 'Steps are C# methods. Go to definition, refactor and set breakpoints as you would in any other code.'],
  ['Rich arguments', 'Pass enums, models and collections into steps. Step text formats them, and you can register custom converters.'],
  ['Isolated by default', 'Each test gets its own Context object, so scenarios run in parallel without shared state.'],
  ['Any runner', 'Adapters for TUnit, NUnit, xUnit and MSTest, or run standalone with BDTestBuilder.'],
  ['Readable reports', 'Stories, scenarios, steps, output and exceptions in one HTML file or on a shared report server.'],
  ['Built for real suites', 'Scenario outlines, tags, timeouts, retries, step hooks and skip rules.'],
];

function Reasons() {
  return (
    <section className={`${styles.container} ${styles.reasons}`} aria-labelledby="reasons-heading">
      <Heading as="h2" id="reasons-heading" className={styles.sectionTitle}>A code-first alternative to SpecFlow</Heading>
      <ul className={styles.reasonList}>
        {reasons.map(([title, text]) => (
          <li key={title}>
            <span className={styles.reasonTick} aria-hidden="true" />
            <div>
              <Heading as="h3">{title}</Heading>
              <p>{text}</p>
            </div>
          </li>
        ))}
      </ul>
    </section>
  );
}

function ReportServer() {
  const screenshot = useBaseUrl('/img/report-server-test-runs.png');
  return (
    <section className={styles.server} aria-labelledby="server-heading">
      <div className={`${styles.container} ${styles.serverGrid}`}>
        <div>
          <Heading as="h2" id="server-heading" className={styles.sectionTitle}>Share a link, not a log file.</Heading>
          <p>
            Host the BDTest report server in any ASP.NET Core app. Test runs upload their results,
            and you get a URL to send to your team. Compare runs, spot flaky tests and follow trends
            over time.
          </p>
          <div className={styles.serverLinks}>
            <Link className={styles.textLink} to="/docs/report-server/overview">Report server guide</Link>
            <Link className={styles.textLink} to="/docs/reports/html-report">Single-file HTML report</Link>
          </div>
        </div>
        <img className={styles.serverShot} src={screenshot} alt="BDTest report server showing a list of test runs with pass and fail counts" loading="lazy" />
      </div>
    </section>
  );
}

export default function Home() {
  return (
    <Layout title="Behaviour-driven testing for .NET" description="BDTest is a code-only behaviour-driven testing and reporting framework for .NET.">
      <main className={styles.home}>
        <Hero />
        <Walkthrough />
        <Reasons />
        <ReportServer />
      </main>
    </Layout>
  );
}
