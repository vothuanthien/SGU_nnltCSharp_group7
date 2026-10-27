using Library.Domain;
var policy=new LoanPolicy(14,5,1000m);
policy.ValidateRequest(3,2);
AssertThrows(()=>policy.ValidateRequest(4,2));
AssertThrows(()=>policy.ValidateRequest(0,0));
if(policy.OverdueDays(new DateOnly(2026,10,1),new DateOnly(2026,10,4))!=3) throw new Exception("Overdue day calculation failed");
if(policy.Fine(new DateOnly(2026,10,1),new DateOnly(2026,10,4))!=3000m) throw new Exception("Fine calculation failed");
Console.WriteLine("Passed: loan limits, non-empty loan, overdue days, configurable fine.");
static void AssertThrows(Action action){try{action();throw new Exception("Expected policy rejection.");}catch(InvalidOperationException){}}
