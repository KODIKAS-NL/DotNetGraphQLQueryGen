grammar GraphQLSchema;

// This is our expression language
schema      : (schemaDef | typeDef | scalarDef | inputDef | enumDef)+;

schemaDef   : comment* 'schema' ws* objectDef;
typeDef     : comment* 'type ' ws* typeName=NAME ws* objectDef;
scalarDef   : comment* 'scalar' ws* typeName=NAME ws+;
inputDef    : comment* 'input' ws* typeName=NAME ws* objectDef;
enumDef     : comment* 'enum' ws* typeName=NAME ws* '{' ws* enumItem+ ws* '}';

objectDef   : '{' ws* fieldDef (ws* fieldDef)* ws* '}' ws*;

fieldDef    : comment* name=NAME ('(' args=arguments ws* ')')? ws* ':' ws* type=dataType required='!'? (ws* '=' ws* value=(INT | NAME))?;
enumItem    : comment* name=NAME ws*;
arguments   : ws* argument (ws* ','? ws* argument)*;
argument    : comment* ws* NAME ws* ':' ws* dataType required='!'?  (ws* '=' ws* value=(INT | NAME))?;

dataType    : (NAME | '[' NAME '!'? ']');
NAME        : [a-z_A-Z] [a-z_A-Z0-9-]*;
INT         : NEGATIVE_SIGN? '0' | NEGATIVE_SIGN? NONZERO_DIGIT DIGIT*;

comment     : ws* (('"' ~('\n'|'\r')* '"') | ('"""' ~'"""'* '"""') | ('#' ~('\n'|'\r')*)) ws*;

ws  : ' ' | '\t' | '\n' | '\r';

fragment NONZERO_DIGIT: [1-9];
fragment DIGIT: [0-9];
fragment NEGATIVE_SIGN: '-';