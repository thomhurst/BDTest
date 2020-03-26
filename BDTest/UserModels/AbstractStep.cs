namespace BDTest.UserModels
{
    public class AbstractStep<TContext>
    {
        protected TContext Context;

        public AbstractStep()
        {
            
        }
        
        internal void InitialiseContext(TContext context)
        {
            Context = context;
        }
    }
}