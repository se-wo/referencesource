//-----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------

namespace System.Activities.Expressions
{
    using System.Activities.Statements;
    using System.Activities.Validation;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Runtime.Collections;
    using System.Windows.Markup;
    using System.Runtime;
    using System.Reflection;
    using System.Threading;

    [ContentProperty("Parameters")]
    public sealed class InvokeMethod<TResult> : AsyncCodeActivity<TResult>
    {
        Collection<Argument> parameters;
        Collection<Type> genericTypeArguments;

        MethodResolver methodResolver;
        MethodExecutor methodExecutor;
        RuntimeArgument resultArgument;

        static MruCache<MethodInfo, Func<object, object[], object>> funcCache =
            new MruCache<MethodInfo, Func<object, object[], object>>(MethodCallExpressionHelper.FuncCacheCapacity);
        static ReaderWriterLockSlim locker = new ReaderWriterLockSlim();

        public Collection<Type> GenericTypeArguments
        {
            get
            {
                if (this.genericTypeArguments == null)
                {
                    this.genericTypeArguments = new ValidatingCollection<Type>
                    {
                        // disallow null values
                        OnAddValidationCallback = item =>
                        {
                            if (item == null)
                            {
                                throw FxTrace.Exception.ArgumentNull("item");
                            }
                        }
                    };
                }
                return this.genericTypeArguments;
            }
        }

        public string MethodName
        {
            get;
            set;
        }

        public Collection<Argument> Parameters
        {
            get
            {
                if (this.parameters == null)
                {
                    this.parameters = new ValidatingCollection<Argument>
                    {
                        // disallow null values
                        OnAddValidationCallback = item =>
                        {
                            if (item == null)
                            {
                                throw FxTrace.Exception.ArgumentNull("item");
                            }
                        }
                    };
                }
                return this.parameters;
            }
        }

        [DefaultValue(null)]
        public InArgument TargetObject
        {
            get;
            set;
        }

        [DefaultValue(null)]
        public Type TargetType
        {
            get;
            set;
        }

        [DefaultValue(false)]
        public bool RunAsynchronously
        {
            get;
            set;
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            Collection<RuntimeArgument> arguments = new Collection<RuntimeArgument>();

            Type targetObjectType = TypeHelper.ObjectType;

            if (this.TargetObject != null)
            {
                targetObjectType = this.TargetObject.ArgumentType;
            }

            RuntimeArgument targetObjectArgument = new RuntimeArgument("TargetObject", targetObjectType, ArgumentDirection.In);
            metadata.Bind(this.TargetObject, targetObjectArgument);
            arguments.Add(targetObjectArgument);

            this.resultArgument = new RuntimeArgument("Result", typeof(TResult), ArgumentDirection.Out);
            metadata.Bind(this.Result, this.resultArgument);
            arguments.Add(this.resultArgument);

            // Parameters are named according to MethodInfo name if DetermineMethodInfo 
            // succeeds, otherwise arbitrary names are used.
            this.methodResolver = CreateMethodResolver();
            
            this.methodResolver.DetermineMethodInfo(metadata, funcCache, locker, ref this.methodExecutor);
             this.methodResolver.RegisterParameters(arguments);

            metadata.SetArgumentsCollection(arguments);

            this.methodResolver.Trace();

            if (this.methodExecutor != null)
            {
                this.methodExecutor.Trace(this);
            }
        }

        protected override IAsyncResult BeginExecute(AsyncCodeActivityContext context, AsyncCallback callback, object state)
        {
            return this.methodExecutor.BeginExecuteMethod(context, callback, state);
        }

        protected override TResult EndExecute(AsyncCodeActivityContext context, IAsyncResult result)
        {
            this.methodExecutor.EndExecuteMethod(context, result);
            return this.Result.Get(context);
        }

        MethodResolver CreateMethodResolver()
        {
            return new MethodResolver
                {
                    MethodName = this.MethodName,
                    RunAsynchronously = this.RunAsynchronously,
                    TargetType = this.TargetType,
                    TargetObject = this.TargetObject,
                    GenericTypeArguments = this.GenericTypeArguments,
                    Parameters = this.Parameters,
                    Result = this.resultArgument,
                    ResultType = typeof(TResult),
                    Parent = this
                };            
        }
    }
}
