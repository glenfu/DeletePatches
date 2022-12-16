The steps to follow to use this tool are:

1. Register app in AAD of target org. Get ClientId and ClientSecret for the app
   Doc to refer: https://docs.microsoft.com/en-us/powerapps/developer/data-platform/walkthrough-register-app-azure-active-directory

2. Also add the app as application users in respective org. 
   Doc to refer: https://docs.metallic.io/metallic/136337_create_application_user_for_dynamics_365.html

3. Add OrgUrl, TenantId, ClientId, ClientSecret for target org in runtimeconfig file.
   Also add parentsolutionid of solution patches to be deleted

4. Build the solution and run the tool. 
Note: If you need to run the deletion of the patches for another base solution, you would need to rebuild with the new parentsolutionid specified.