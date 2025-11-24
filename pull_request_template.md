# DEFINITION OF DONE - PULL REQUEST CHECKLIST

## CODE QUALITY
- [ ] Code review uitgevoerd door het andere teamlid
- [ ] Code volgt dotnet coding standards en conventions
- [ ] Geen obvious code smells of technical debt toegevoegd
- [ ] Clean code principles toegepast (readable, maintainable)
- [ ] Geen dead code of commented-out code blocks
- [ ] Variabelen en functies hebben duidelijke namen
- [ ] Code is consistent met bestaande codebase style
- [ ] Architecture patronen volgens onderzoeksplan correct toegepast

## FUNCTIONALITY
- [ ] Alle acceptatie criteria uit de user story zijn geïmplementeerd
- [ ] Feature werkt volgens specificaties
- [ ] Happy flow scenario volledig werkend
- [ ] Edge cases geïdentificeerd en afgehandeld
- [ ] Geen regressies in bestaande functionaliteit
- [ ] Error handling geïmplementeerd voor failure scenarios

## TESTING
- [ ] Unit tests geschreven voor nieuwe functionaliteit
- [ ] Bestaande tests blijven slagen
- [ ] Test coverage acceptabel (+/- 60%) voor nieuwe code
- [ ] Integration tests toegevoegd waar nodig
- [ ] Manual testing uitgevoerd waar mogelijk
- [ ] Performance impact getest voor grote wijzigingen
- [ ] Edge cases getest

##### TEST GUIDELINES
- [ ] Happy Path: 1 test voor normale uitvoering
- [ ] Edge Cases: 1-3 tests voor grensgevallen
- [ ] Error Scenarios: 1-2 tests voor exception handling
- [ ] Branch Coverage: Tests voor elke if/else, switch case

## SECURITY
- [ ] Authentication en authorization correct toegepast
- [ ] Geen hardcoded secrets of credentials
- [ ] Environment variables gebruikt voor gevoelige configuratie
- [ ] Secure protocols gebruikt voor externe communicatie

## ARCHITECTURE & DESIGN PATTERNS
- [ ] Architectuur patterns correct toegepast
- [ ] Design patterns consistent met project architectuur
- [ ] Service boundaries gerespecteerd
- [ ] API contracts niet gebroken zonder versioning
- [ ] Database schema wijzigingen backwards compatible
- [ ] Microservice patterns gevolgd waar van toepassing
- [ ] Event-driven architectuur correct gebruikt

## PERFORMANCE
- [ ] Response times binnen acceptabele grenzen
- [ ] Resource usage geoptimaliseerd
- [ ] Database queries efficiënt uitgevoerd
- [ ] Caching strategieën toegepast waar relevant

## DOCUMENTATION
- [ ] Configuration changes gedocumenteerd
- [ ] API/technical documentation bijgewerkt
- [ ] Deployment procedures aangepast waar nodig
- [ ] README files up-to-date
- [ ] Portfolio aangepast met competentie bewijs

## CLEANUP
- [ ] Temporary code en debug statements verwijderd (Ook Console log statements)
- [ ] Test files en mock data opgeruimd
- [ ] Unused imports/dependencies verwijderd
- [ ] Formatting consistent door hele codebase

## VERSION CONTROL
- [ ] Commit messages descriptief
- [ ] Geen merge conflicts
- [ ] Branch up-to-date met target branch
- [ ] Pull request description volledig ingevuld

## FINAL CHECKS & SIGN-OFF
- [ ] Code reviewer approval (andere teamlid)
- [ ] Code reviewer approval (bedrijfsbegeleider)
- [ ] Alle test passed
- [ ] Handmatige test completed (waar mogelijk)
- [ ] Documentation updated

**MERGE CRITERIA:** Alle relevante checkboxes afgevinkt EN goedkeuring van code reviewer
- [ ] Ready to merge
