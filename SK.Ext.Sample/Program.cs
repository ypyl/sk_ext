using SK.Ext.Sample;

const string groqKey = "<<KEY>>";

//await new ParallelExecutionSample().Run(groqKey);
await new StructuredOutputSample().Run(groqKey);
