Request-Body:
{
"query":"<Cosmos Query>",
"limit":500, //Number of records it should change
"includeDependedQuery": true, //Default Value: False. Use When there is pre-condition to check.
"requiredChange":
	{
		"<field-name1>": "<field-value1>",
    "<field-name2>": "<field-value2>",
    .
    .
    .
	}
}
