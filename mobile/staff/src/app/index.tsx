import { useEffect, useState } from 'react';
import { ActivityIndicator, Pressable, StyleSheet, TextInput, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { ThemedText } from '@/components/themed-text';
import { ThemedView } from '@/components/themed-view';
import { AccessContext, getAccessContext, hasStoredSession, signIn, signOut } from '@/lib/auth-client';

const features=[
  {label:'My school',permission:'Schools.View',entitlement:'school-administration'},
  {label:'Classes',permission:'Academics.View',entitlement:'academic-structure'},
  {label:'Staff directory',permission:'Staff.View',entitlement:'staff-management'},
  {label:'Students',permission:'Students.View',entitlement:'student-information'},
];

export default function StaffHome() {
  const [email,setEmail]=useState(''); const [password,setPassword]=useState(''); const [tenantId,setTenantId]=useState(''); const [campusId,setCampusId]=useState(''); const [busy,setBusy]=useState(true); const [message,setMessage]=useState('Restoring your secure session…'); const [access,setAccess]=useState<AccessContext|null>(null);
  useEffect(()=>{ void (async()=>{ try { if(await hasStoredSession()){setAccess(await getAccessContext());setMessage('Your secure session was restored.');}else setMessage('Sign in with your school membership.'); }catch(error){await signOut();setMessage(error instanceof Error?error.message:'Your session could not be restored.');}finally{setBusy(false);} })(); },[]);
  async function login(){setBusy(true);try{await signIn(email,password,tenantId,campusId);setAccess(await getAccessContext());setPassword('');setMessage('Connected with server-enforced school and campus access.');}catch(error){setMessage(error instanceof Error?error.message:'Sign-in failed.');}finally{setBusy(false);}}
  async function logout(){await signOut();setAccess(null);setPassword('');setMessage('You have signed out securely.');}
  const hasStaffAudience=access?.audiences.some(value=>value==='Teacher'||value==='Staff')===true;
  const available=features.filter(feature=>hasStaffAudience&&access?.permissions.includes(feature.permission)&&access.entitlements[feature.entitlement]?.enabled);
  return <ThemedView style={styles.screen}><SafeAreaView style={styles.safe}><View style={styles.header}><ThemedText type="title">Staff workspace</ThemedText><ThemedText>Your school tools, scoped to your role and campus.</ThemedText></View>{busy&&!access?<ActivityIndicator/>:!access?<View style={styles.card}><TextInput autoCapitalize="none" keyboardType="email-address" placeholder="Email" value={email} onChangeText={setEmail} style={styles.input}/><TextInput placeholder="Password" secureTextEntry value={password} onChangeText={setPassword} style={styles.input}/><TextInput autoCapitalize="none" placeholder="School workspace ID" value={tenantId} onChangeText={setTenantId} style={styles.input}/><TextInput autoCapitalize="none" placeholder="Campus ID (optional)" value={campusId} onChangeText={setCampusId} style={styles.input}/><Pressable disabled={busy||!email||!password||!tenantId} onPress={()=>void login()} style={styles.button}>{busy?<ActivityIndicator color="#fff"/>:<ThemedText style={styles.buttonText}>Sign in</ThemedText>}</Pressable></View>:<View style={styles.card}><ThemedText type="subtitle">Staff access connected</ThemedText><View style={styles.row}>{available.length?available.map(feature=><View key={feature.label} style={styles.tile}><ThemedText type="smallBold">{feature.label}</ThemedText><ThemedText type="small">Available for your role</ThemedText></View>):<ThemedText type="small">No Staff features are currently enabled for this account.</ThemedText>}</View><Pressable onPress={()=>void logout()} style={styles.secondary}><ThemedText>Sign out</ThemedText></Pressable></View>}<ThemedText type="small">{message}</ThemedText></SafeAreaView></ThemedView>;
}
const styles=StyleSheet.create({screen:{flex:1},safe:{flex:1,padding:24,gap:20},header:{gap:8,paddingTop:32},card:{backgroundColor:'#E4E9FF',borderRadius:24,padding:22,gap:12},input:{backgroundColor:'#fff',borderColor:'#94A3B8',borderWidth:1,borderRadius:12,padding:14,color:'#0F172A'},button:{backgroundColor:'#3730A3',borderRadius:12,padding:14,alignItems:'center'},buttonText:{color:'#fff'},secondary:{borderWidth:1,borderColor:'#64748B',borderRadius:12,padding:12,alignItems:'center'},row:{gap:12},tile:{borderWidth:1,borderColor:'#CBD5E1',borderRadius:18,padding:18,gap:4}});
